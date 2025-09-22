using UnityEngine;

public class BotSpawner_Proto : MonoBehaviour
{
    public GameObject botPrefab;     // Bot prefab (Bot_Proto)
    public Transform[] spawnPoints;  // Lista de pontos de spawn
    public Transform[] patrolWaypoints; // Waypoints específicos para patrulha
    public int count = 3;            // Quantos bots serão instanciados

    void Start(){
        // Verifica se o botPrefab e spawnPoints estão configurados
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
            // Escolhe um ponto de spawn (se tiver mais spawn points que bots, eles serão distribuídos)
            var spawnPoint = spawnPoints[i % spawnPoints.Length];  
            var bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);  // Instancia o bot
            bot.name = $"Bot_{i + 1}";  // Dê um nome único ao bot (Bot_1, Bot_2, etc.)

            // Atribui os Patrol Points ao bot recém instanciado
            var ai = bot.GetComponent<BotAI_Proto>();  // Obtém o componente de IA do bot
            if (ai != null)
            {
                // Verifica se os Patrol Points não foram atribuídos no prefab
                if (ai.patrolPoints == null || ai.patrolPoints.Length == 0)
                {
                    ai.patrolPoints = patrolWaypoints;  // Atribui os waypoints ao bot
                }
            }
        }
        
        Debug.Log($"Spawned {count} bots!");
    }
}
