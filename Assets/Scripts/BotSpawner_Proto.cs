using UnityEngine;

public class BotSpawner_Proto : MonoBehaviour
{
    public GameObject botPrefab;
    public Transform[] spawnPoints;
    public Transform[] patrolWaypoints;
    public int count = 3;

    void Start(){
        if (botPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("Configura botPrefab e spawnPoints.");
            return;
        }

        SpawnBots();
    }

    void SpawnBots()
    {
        for (int i = 0; i < count; i++)
        {
            var spawnPoint = spawnPoints[i % spawnPoints.Length];  
            var bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);
            bot.name = $"Bot_{i + 1}";
            
            var ai = bot.GetComponent<BotAI_Proto>();
            if (ai != null)
            {
                if (ai.patrolPoints == null || ai.patrolPoints.Length == 0)
                {
                    ai.patrolPoints = patrolWaypoints;
                }
            }
        }
        
        Debug.Log($"Spawned {count} bots.");
    }
}
