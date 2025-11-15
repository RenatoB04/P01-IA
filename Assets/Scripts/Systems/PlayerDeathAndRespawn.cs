using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components; // necessário para NetworkTransform
using System;

public class PlayerDeathAndRespawn : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkTransform netTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Health health;

    [Header("Spawn Points Fixos (Mundiais)")]
    [Tooltip("SpawnPoint A (por exemplo lado esquerdo do mapa).")]
    [SerializeField] private Vector3 spawnPointA = new Vector3(87f, 1.5f, 115f);

    [Tooltip("SpawnPoint B (por exemplo lado direito do mapa).")]
    [SerializeField] private Vector3 spawnPointB = new Vector3(87f, 1.5f, 175f);

    [Header("Offset/Segurança")]
    [Tooltip("Offset vertical aplicado acima do ponto de spawn.")]
    [SerializeField] private float spawnUpOffset = 1.5f;
    [Tooltip("Raycast para ajustar o spawn ao chão (recomendado).")]
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

        if (!netTransform)
            netTransform = GetComponentInChildren<NetworkTransform>();

        // SPAWN INICIAL: servidor decide e **força** o dono a teletransportar via ClientRpc.
        if (IsServer)
        {
            var spawn = ResolveSpawnForOwner(OwnerClientId);
            Debug.Log($"[Respawn] OnNetworkSpawn → Spawn inicial. Owner={OwnerClientId}, SpawnPos={spawn.pos}");
            ForceOwnerTeleportServer(spawn.pos, spawn.rot);
        }
    }

    /// <summary>
    /// RPC de respawn, usado quando o jogador morre.
    /// Para spawn inicial forçado, usar ignoreAliveCheck = true.
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
        Debug.Log($"[Respawn] Respawn/Spawn no servidor. Owner={OwnerClientId} SpawnPos={spawn.pos}");

        health.ResetFullHealth();
        ForceOwnerTeleportServer(spawn.pos, spawn.rot);
    }

    /// <summary>
    /// Força o dono a teletransportar-se. 
    /// - Se o servidor tiver autoridade sobre o NetworkTransform, também teleporta no servidor (para consistência).
    /// - **Mas em qualquer caso** envia um ClientRpc dirigido ao dono (Owner) para garantir o movimento.
    /// </summary>
    private void ForceOwnerTeleportServer(Vector3 spawnPos, Quaternion spawnRot)
    {
        // 1) Se o servidor puder “commit” (server authority), teleporta também no servidor.
        try
        {
            if (netTransform != null && netTransform.CanCommitToTransform)
            {
                Vector3 scale = transform.localScale;
                netTransform.Teleport(spawnPos, spawnRot, scale);
            }
            else
            {
                // Mesmo sem autoridade, não há problema — o passo 2 vai resolver no Owner.
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Respawn] Server-side Teleport falhou/sem autoridade: {ex.Message}. A prosseguir com RPC ao Owner.");
        }

        // 2) **Sempre** enviar RPC dirigido ao Owner, que tem autoridade e pode aplicar a pose.
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
                // No Owner, a autoridade é local — este Teleport *vai* aplicar.
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
            Debug.LogError($"[Respawn] Owner Teleport falhou: {ex.Message}. Fallback transform.SetPositionAndRotation.");
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
        }

        GameplayCursor.Lock();

        if (ccWasEnabled) characterController.enabled = true;
    }

    // ===================== LÓGICA DE RESOLUÇÃO DE SPAWN =====================

    private Pose ResolveSpawnForOwner(ulong ownerClientId)
    {
        // Fallback simples e determinístico A/B (sem dependências externas).
        if (spawnPointA == Vector3.zero && spawnPointB == Vector3.zero)
        {
            spawnPointA = new Vector3(-5f, spawnUpOffset, 0f);
            spawnPointB = new Vector3(5f, spawnUpOffset, 0f);
        }

        bool useA = (ownerClientId % 2UL == 0UL);
        var basePos = useA ? spawnPointA : spawnPointB;
        var rot = Quaternion.identity;

        var final = FinalizePose(basePos, rot);
        Debug.Log($"[Respawn] ResolveSpawn → Owner={ownerClientId}, useA={useA}, basePos={basePos}, finalPos={final.pos}");
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
