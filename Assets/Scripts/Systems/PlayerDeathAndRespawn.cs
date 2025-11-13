using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components; // NetworkTransform
using System;

/// <summary>
/// Gere morte e respawn do jogador.
/// - O servidor escolhe o ponto de spawn.
/// - O dono é teletransportado via RPC (owner authority).
/// </summary>
public class PlayerDeathAndRespawn : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkTransform netTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Health health;

    [Header("Spawn Points Fixos (backup)")]
    [Tooltip("SpawnPoint A (fallback se não houver SpawnPointsProvider).")]
    [SerializeField] private Vector3 spawnPointA = new Vector3(-5f, 1.5f, 0f);

    [Tooltip("SpawnPoint B (fallback se não houver SpawnPointsProvider).")]
    [SerializeField] private Vector3 spawnPointB = new Vector3(5f, 1.5f, 0f);

    [Header("Offset/Segurança")]
    [Tooltip("Offset vertical aplicado acima do ponto de spawn.")]
    [SerializeField] private float spawnUpOffset = 1.5f;
    [Tooltip("Raycast para ajustar o spawn ao chão.")]
    [SerializeField] private bool groundSnap = true;
    [SerializeField] private float groundRaycastUp = 2f;
    [SerializeField] private float groundRaycastDown = 10f;

    private struct Pose
    {
        public Vector3 pos;
        public Quaternion rot;
        public Pose(Vector3 p, Quaternion r) { pos = p; rot = r; }
    }

    private void Awake()
    {
        if (!netTransform) netTransform = GetComponentInChildren<NetworkTransform>();
        if (!characterController) characterController = GetComponentInChildren<CharacterController>();
        if (!health) health = GetComponentInChildren<Health>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Só garantimos refs aqui. O spawn INICIAL é feito
        // por NetworkSpawnHandler → RespawnServerRpc(true).
        if (!netTransform)
            netTransform = GetComponentInChildren<NetworkTransform>();

        Debug.Log($"[Respawn] OnNetworkSpawn em {name}. IsServer={IsServer}, Owner={OwnerClientId}");
    }

    /// <summary>
    /// Chamado pelo dono (ou por scripts de respawn) para fazer spawn/respawn.
    /// ignoreAliveCheck = true → usado para o spawn inicial.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RespawnServerRpc(bool ignoreAliveCheck = false, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (health == null)
        {
            Debug.LogError("[Respawn] Health nulo no servidor.");
            return;
        }

        if (!ignoreAliveCheck && !health.isDead.Value)
        {
            Debug.LogWarning("[Respawn] Ignorado: jogador não está morto.");
            return;
        }

        var spawn = ResolveSpawnForOwner(OwnerClientId);
        Debug.Log($"[Respawn] Respawn no servidor. Owner={OwnerClientId} SpawnPos={spawn.pos}");

        health.ResetFullHealth();
        ForceOwnerTeleportServer(spawn.pos, spawn.rot);
    }

    /// <summary>
    /// No servidor: tenta teletransportar, depois manda RPC para o dono.
    /// </summary>
    private void ForceOwnerTeleportServer(Vector3 spawnPos, Quaternion spawnRot)
    {
        // 1) Se o servidor tiver autoridade sobre o NetworkTransform, teleporta também aqui.
        try
        {
            if (netTransform != null && netTransform.CanCommitToTransform)
            {
                Vector3 scale = transform.localScale;
                netTransform.Teleport(spawnPos, spawnRot, scale);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Respawn] Server-side Teleport falhou/sem autoridade: {ex.Message}");
        }

        // 2) Diz ao dono para se teletransportar localmente (authority do owner).
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        OwnerTeleportClientRpc(spawnPos, spawnRot, transform.localScale, target);
    }

    [ClientRpc]
    private void OwnerTeleportClientRpc(Vector3 pos, Quaternion rot, Vector3 scale, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        bool ccWasEnabled = characterController && characterController.enabled;
        if (ccWasEnabled) characterController.enabled = false;

        try
        {
            if (netTransform != null)
            {
                netTransform.Teleport(pos, rot, scale);
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
                transform.localScale = scale;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Respawn] Owner Teleport falhou: {ex.Message}. Fallback transform.");
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
        }

        // Volta a trancar o cursor no respawn.
        GameplayCursor.Lock();

        if (ccWasEnabled) characterController.enabled = true;
    }

    // =====================================================================
    // LÓGICA DE ESCOLHA DE SPAWN
    // =====================================================================

    private Pose ResolveSpawnForOwner(ulong ownerClientId)
    {
        // 1) Se houver SpawnPointsProvider na cena, usamos isso.
        var provider = SpawnPointsProvider.Instance;

        if (provider != null)
        {
            bool useA = (ownerClientId % 2UL == 0UL);
            Vector3 basePos;
            Quaternion baseRot;
            bool ok = useA
                ? provider.TryGetSpawnA(out basePos, out baseRot)
                : provider.TryGetSpawnB(out basePos, out baseRot);

            // Se o spawn escolhido falhar, tenta o outro.
            if (!ok)
            {
                bool otherOk = !useA
                    ? provider.TryGetSpawnA(out basePos, out baseRot)
                    : provider.TryGetSpawnB(out basePos, out baseRot);

                if (!otherOk)
                {
                    // Falhou tudo → passa para fallback de Vector3.
                    Debug.LogWarning("[Respawn] SpawnPointsProvider não devolveu posição válida. A usar fallback.");
                    return ResolveFallbackSpawn(ownerClientId);
                }
            }

            var finalFromProvider = FinalizePose(basePos, baseRot);
            Debug.Log($"[Respawn] ResolveSpawn (Provider) Owner={ownerClientId}, useA={useA}, finalPos={finalFromProvider.pos}");
            return finalFromProvider;
        }

        // 2) Caso não haja provider, usa os Vector3 configurados no inspector.
        return ResolveFallbackSpawn(ownerClientId);
    }

    private Pose ResolveFallbackSpawn(ulong ownerClientId)
    {
        bool useA = (ownerClientId % 2UL == 0UL);

        // Se por acaso estiverem a zero absolutos, mete uns valores minimamente decentes.
        if (spawnPointA == Vector3.zero && spawnPointB == Vector3.zero)
        {
            spawnPointA = new Vector3(-5f, spawnUpOffset, 0f);
            spawnPointB = new Vector3(5f, spawnUpOffset, 0f);
        }

        var basePos = useA ? spawnPointA : spawnPointB;
        var rot = Quaternion.identity;

        var final = FinalizePose(basePos, rot);
        Debug.Log($"[Respawn] ResolveFallbackSpawn Owner={ownerClientId}, useA={useA}, basePos={basePos}, finalPos={final.pos}");
        return final;
    }

    private Pose FinalizePose(Vector3 basePos, Quaternion rot)
    {
        var pos = basePos + Vector3.up * Mathf.Max(0.1f, spawnUpOffset);
        SafeSnapToGround(ref pos);
        return new Pose(pos, rot);
    }

    private void SafeSnapToGround(ref Vector3 pos)
    {
        if (!groundSnap) return;

        Vector3 origin = pos + Vector3.up * Mathf.Max(0.01f, groundRaycastUp);

        if (Physics.Raycast(origin, Vector3.down, out var hit,
                Mathf.Max(groundRaycastDown, spawnUpOffset + 2f),
                ~0, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point + Vector3.up * Mathf.Max(0.1f, spawnUpOffset);
        }
    }
}
