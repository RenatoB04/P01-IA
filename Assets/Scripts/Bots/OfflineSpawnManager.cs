using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class OfflineSpawnManager : MonoBehaviour
{
    [SerializeField] Transform spawnPoint;

    IEnumerator Start()
    {
        
        if (PlayerPrefs.GetInt("OfflineMode", 0) != 1)
            yield break;

        
        GameObject localPlayer = null;
        for (int i = 0; i < 120 && localPlayer == null; i++)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            {
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    localPlayer = go;
                    break;
                }
            }
            yield return null; 
        }

        
        PlayerPrefs.SetInt("OfflineMode", 0);

        if (localPlayer != null && spawnPoint != null)
        {
            localPlayer.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
        else
        {
            Debug.LogWarning("OfflineSpawnManager: n�o encontrou player ou spawnPoint.");
        }
    }
}