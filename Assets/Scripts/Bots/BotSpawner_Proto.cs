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

    int nextSpawnIndex = 0;
    int spawnedTotal = 0;

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
    // Spawn unitário (CRÍTICO: Injeção de Waypoints e Link de Eventos)
    // ------------------------------------------------------------
    void SpawnOne()
    {
        var spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        nextSpawnIndex++;

        // 1. Instanciar
        var bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);
        spawnedTotal++;
        bot.name = $"Bot_{spawnedTotal}";

        // 2. ATRIBUIR WAYPOINTS AO BotAI_Proto
        // O bot vai iniciar a patrulha automaticamente no seu método OnEnable()
        var ai = bot.GetComponent<BotAI_Proto>();
        if (ai != null)
        {
            // Apenas atribuimos os waypoints. O bot tratará do resto.
            ai.patrolPoints = patrolWaypoints;
        }

        // 3. Ligar o script BotRespawnLink e Eventos
        var link = bot.GetComponent<BotRespawnLink>();
        if (!link) link = bot.AddComponent<BotRespawnLink>();
        link.spawner = this;
        link.patrolWaypoints = patrolWaypoints; // Passa os waypoints para o link também, se necessário ao recriar

        var death = bot.GetComponent<BOTDeath>();
        if (death != null)
        {
            // Usa o evento do BOTDeath para iniciar o respawn
            death.OnDied -= HandleBotDied; // Garante que não subscreve múltiplas vezes
            death.OnDied += HandleBotDied;
        }
        else
        {
            Debug.LogWarning($"[BotSpawner_Proto] Bot '{bot.name}' não tem BOTDeath. Sem respawn automático via evento.");
        }
    }

    // ------------------------------------------------------------
    // Evento chamado quando um bot morre (subscrito ao BOTDeath.OnDied)
    // ------------------------------------------------------------
    void HandleBotDied(BOTDeath deadBotScript) // O evento passa o script que morreu
    {
        // Importante: Remover a subscrição para evitar chamadas múltiplas se algo der errado
        if (deadBotScript != null)
        {
             deadBotScript.OnDied -= HandleBotDied;
        }
       
        // Inicia a rotina para fazer respawn após o delay
        StartCoroutine(RespawnRoutine());
    }

    // ------------------------------------------------------------
    // Coroutine interna usada para respawn
    // ------------------------------------------------------------
    IEnumerator RespawnRoutine()
    {
        // Espera o tempo definido
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        // Cria um novo bot
        SpawnOne();
    }

    // ------------------------------------------------------------
    // Método público chamado pelo BotRespawnLink (Manter para compatibilidade, se necessário)
    // Embora o ideal seja usar o evento HandleBotDied
    // ------------------------------------------------------------
    public void ScheduleRespawn(Transform[] waypointsFromDead)
    {
        // Usa o fluxo de respawn já existente baseado em Coroutine
        StartCoroutine(RespawnRoutine());
    }
}