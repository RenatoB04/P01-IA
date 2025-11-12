using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class SimpleSpawnByClientId : NetworkBehaviour
{
    [Header("Spawn Points Fixos (Mundiais)")]
    [SerializeField] private Vector3 spawnPointA = new Vector3(-10f, 2f, 0f);
    [SerializeField] private Vector3 spawnPointB = new Vector3(10f, 2f, 0f);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Só o servidor decide o spawn.
        if (!IsServer) return;

        // Jogadores com OwnerClientId par → A, ímpar → B.
        bool useA = (OwnerClientId % 2 == 0);
        Vector3 targetPos = useA ? spawnPointA : spawnPointB;
        Quaternion targetRot = Quaternion.identity;

        // Tenta usar NetworkTransform, se existir.
        if (TryGetComponent<NetworkTransform>(out var netTransform))
        {
            netTransform.Teleport(targetPos, targetRot, transform.localScale);
        }
        else
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }

        Debug.Log($"[SimpleSpawnByClientId] Owner={OwnerClientId}, useA={useA}, spawn={targetPos}");
    }
}