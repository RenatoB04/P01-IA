using Unity.Netcode;
using UnityEngine;
using System.Collections; // Necessário para Coroutines

[RequireComponent(typeof(PlayerDeathAndRespawn))]
public class NetworkSpawnHandler : NetworkBehaviour
{
    private PlayerDeathAndRespawn respawnController;

    void Awake()
    {
        respawnController = GetComponent<PlayerDeathAndRespawn>();
        
        if (respawnController == null)
        {
            Debug.LogError("NetworkSpawnHandler: Falha ao encontrar PlayerDeathAndRespawn. Verifique o Prefab.");
        }
    }

    public override void OnNetworkSpawn() 
    {
        base.OnNetworkSpawn();
        
        // Apenas o dono (jogador local) pede ao servidor o spawn inicial.
        if (IsOwner && respawnController != null)
        {
            // Pequeno atraso de 1 frame para garantir que RPCs estão registados.
            StartCoroutine(SafeRespawnCoroutine());
        }
    }
    
    private IEnumerator SafeRespawnCoroutine()
    {
        // Espera 1 frame
        yield return null; 
        
        if (IsSpawned && respawnController != null)
        {
            Debug.Log("[SpawnHandler] A chamar RespawnServerRpc(ignoreAliveCheck: true) para spawn inicial...");
            // true = ignorar o check de morto e usar logo os spawn points
            respawnController.RespawnServerRpc(true);
        }
    }
}