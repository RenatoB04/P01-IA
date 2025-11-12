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

    void Awake()
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

        // SPAWN INICIAL: faz o que o SimpleSpawnByClientId fazia
        if (IsServer)
        {
            GetSpawnPose(out var spawnPos, out var spawnRot);
            Debug.Log($"[Respawn] OnNetworkSpawn → Spawn inicial. Owner={OwnerClientId}, SpawnPos={spawnPos}");

            ServerTeleport(spawnPos, spawnRot);
        }
    }

    /// <summary>
    /// RPC de respawn, usado quando o jogador morre.
    /// Para o spawn inicial, podes chamar com ignoreAliveCheck = true.
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

        // Respawn normal só quando o jogador está morto,
        // a não ser que ignoreAliveCheck venha a true (spawn inicial forçado).
        if (!ignoreAliveCheck && !health.isDead.Value)
        {
            Debug.LogWarning("[Respawn] Ignorado: jogador não está morto.");
            return;
        }

        GetSpawnPose(out Vector3 spawnPos, out Quaternion spawnRot);

        Debug.Log($"[Respawn] Respawn/Spawn no servidor. Owner={OwnerClientId} SpawnPos={spawnPos}");

        // Garantimos vida cheia no respawn / spawn inicial
        health.ResetFullHealth();

        ServerTeleport(spawnPos, spawnRot);
    }

    public void ServerTeleport(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (!IsServer) return;

        bool ccWasEnabled = characterController && characterController.enabled;
        if (ccWasEnabled) characterController.enabled = false;

        Vector3 scale = transform.localScale;

        if (netTransform != null)
        {
            try
            {
                if (netTransform.CanCommitToTransform)
                {
                    netTransform.Teleport(spawnPos, spawnRot, scale);
                    GameplayCursor.Lock();
                }
                else
                {
                    var target = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                    };
                    OwnerTeleportClientRpc(spawnPos, spawnRot, scale, target);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Respawn] Exceção no teleport server: {ex.Message}. Fallback: pedir ao dono.");
                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                };
                OwnerTeleportClientRpc(spawnPos, spawnRot, scale, target);
            }
        }
        else
        {
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            GameplayCursor.Lock();
        }

        if (ccWasEnabled) characterController.enabled = true;
    }

    [ClientRpc]
    private void OwnerTeleportClientRpc(Vector3 pos, Quaternion rot, Vector3 scale, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        bool ccWasEnabled = characterController && characterController.enabled;
        if (ccWasEnabled) characterController.enabled = false;

        if (netTransform != null)
        {
            try
            {
                netTransform.Teleport(pos, rot, scale);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Respawn] Client(owner) Teleport falhou: {ex.Message}. Usando SetPositionAndRotation.");
                transform.SetPositionAndRotation(pos, rot);
                transform.localScale = scale;
            }
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
        }

        GameplayCursor.Lock();

        if (ccWasEnabled) characterController.enabled = true;
    }

    /// <summary>
    /// Decide o ponto de spawn com base no OwnerClientId, tal como o SimpleSpawnByClientId.
    /// Owner par → A, ímpar → B.
    /// </summary>
    private void GetSpawnPose(out Vector3 pos, out Quaternion rot)
    {
        // Se por acaso tiveres ambos a zero, mete-os em algo minimamente afastado.
        if (spawnPointA == Vector3.zero && spawnPointB == Vector3.zero)
        {
            spawnPointA = new Vector3(-5f, spawnUpOffset, 0f);
            spawnPointB = new Vector3(5f, spawnUpOffset, 0f);
        }

        bool useA = (OwnerClientId % 2 == 0); // host / client0 → A, client1 → B
        Vector3 basePos = useA ? spawnPointA : spawnPointB;

        pos = basePos + Vector3.up * Mathf.Max(0.1f, spawnUpOffset);
        rot = Quaternion.identity;

        Debug.Log($"[Respawn] GetSpawnPose (Fixos) Owner={OwnerClientId}, useA={useA}, basePos={basePos}");

        SafeSnapToGround(ref pos);
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
