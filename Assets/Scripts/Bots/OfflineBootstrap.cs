using UnityEngine;

public class OfflineBootstrap : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Transform spawnPoint;

    void Start()
    {
        if (PlayerPrefs.GetInt("OfflineMode", 0) == 1)
        {
            PlayerPrefs.SetInt("OfflineMode", 0); // limpa flag
            Debug.Log("Modo Offline: a criar jogador local e ativar bots.");

            if (playerPrefab && spawnPoint)
                Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            else
                Debug.LogWarning("OfflineBootstrap: faltam referências ao playerPrefab ou spawnPoint.");

            var spawner = FindObjectOfType<BotSpawner_Proto>();
            if (spawner) spawner.enabled = true;
        }
    }
}
