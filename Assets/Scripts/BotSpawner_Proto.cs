using UnityEngine;
using System.Collections;

public class BotSpawner_Proto : MonoBehaviour
{
    [Header("Configuração Inicial")]
    public GameObject botPrefab;
    public Transform[] spawnPoints;
    public Transform[] patrolWaypoints;
    [Tooltip("Quantos bots devem existir em simultâneo.")]
    public int count = 3;

    [Header("Respawn")]
    [Tooltip("Segundos a aguardar após a morte para nascer um novo bot.")]
    public float respawnDelay = 5f;

    int nextSpawnIndex = 0;   // para rodar pelos spawn points
    int spawnedTotal = 0;     // apenas para nomear os bots

    void Start()
    {
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
            SpawnOne();

        Debug.Log($"Spawned {count} bots.");
    }

    // ------------------------------------------------------------
    // Spawn unitário (usado no início e no respawn)
    // ------------------------------------------------------------
    void SpawnOne()
    {
        var spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        nextSpawnIndex++;

        var bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);
        spawnedTotal++;
        bot.name = $"Bot_{spawnedTotal}";

        // Atribuir waypoints à AI, se necessário
        var ai = bot.GetComponent<BotAI_Proto>();
        if (ai != null)
        {
            if (ai.patrolPoints == null || ai.patrolPoints.Length == 0)
                ai.patrolPoints = patrolWaypoints;
        }

        // Ligar o script BotRespawnLink (caso não exista)
        var link = bot.GetComponent<BotRespawnLink>();
        if (!link) link = bot.AddComponent<BotRespawnLink>();
        link.spawner = this;
        link.patrolWaypoints = patrolWaypoints;

        // Ligar ao evento de morte para agendar respawn (fallback direto)
        var death = bot.GetComponent<BOTDeath>();
        if (death != null)
        {
            death.OnDied -= HandleBotDied;
            death.OnDied += HandleBotDied;
        }
        else
        {
            Debug.LogWarning($"[BotSpawner_Proto] Bot '{bot.name}' não tem BOTDeath. Sem respawn automático.");
        }
    }

    // ------------------------------------------------------------
    // Evento chamado quando um bot morre
    // ------------------------------------------------------------
    void HandleBotDied(BOTDeath death)
    {
        // segurança: remove o handler (caso o objeto seja destruído)
        death.OnDied -= HandleBotDied;

        // agenda a reposição de 1 bot para manter o 'count' constante
        StartCoroutine(RespawnRoutine());
    }

    // ------------------------------------------------------------
    // Coroutine interna usada para respawn
    // ------------------------------------------------------------
    IEnumerator RespawnRoutine()
    {
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        SpawnOne();
    }

    // ------------------------------------------------------------
    // Método público chamado pelo BotRespawnLink (para compatibilidade)
    // ------------------------------------------------------------
    public void ScheduleRespawn(Transform[] waypointsFromDead)
    {
        // (Opcional) se quiseres usar os waypoints do bot morto
        if (waypointsFromDead != null && waypointsFromDead.Length > 0)
            this.patrolWaypoints = waypointsFromDead;

        StartCoroutine(RespawnRoutine());
    }
}