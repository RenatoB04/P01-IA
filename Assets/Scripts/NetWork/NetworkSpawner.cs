using Unity.Netcode;
using UnityEngine;
using System.Collections; 

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
        
        
        if (IsOwner && respawnController != null)
        {
            
            StartCoroutine(SafeRespawnCoroutine());
        }
    }
    
    private IEnumerator SafeRespawnCoroutine()
    {
        
        yield return null; 
        
        if (IsSpawned && respawnController != null)
        {
            Debug.Log("[SpawnHandler] A chamar RespawnServerRpc(ignoreAliveCheck: true) para spawn inicial...");
            
            respawnController.RespawnServerRpc(true);
        }
    }
}