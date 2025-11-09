using UnityEngine;
using System.Collections;
using Unity.Netcode; // <-- ADICIONADO

public class BotSpawner_Proto : MonoBehaviour
{
    [Header("Configuração Inicial")]
    public GameObject botPrefab;
    public Transform[] spawnPoints;

    [Tooltip("Waypoints de patrulha que o bot vai usar (podes meter vários paths aqui).")]
    public Transform[] patrolWaypoints;

    [Tooltip("Bots que nascem no início da ronda.")]
    public int count = 3;

    [Header("Limites / Ronda")]
    [Tooltip("Máximo de bots vivos em simultâneo.")]
    public int maxAliveBots = 10;

    [Tooltip("Duração da ronda em segundos (3 min = 180).")]
    public float roundDuration = 180f;

    [Header("Respawn")]
    [Tooltip("Segundos a aguardar após a morte antes de nascerem bots novos.")]
    public float respawnDelay = 2f;

    int nextSpawnIndex = 0;
    int spawnedTotal = 0;
    int aliveBots = 0;

    float roundTimer = 0f;
    bool roundActive = false;

    // ------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------
    void Awake()
    {
        // Só queremos bots quando viemos do botão "Jogar com Bots"
        if (PlayerPrefs.GetInt("OfflineMode", 0) != 1)
        {
            enabled = false;
        }
    }

    void OnEnable()
    {
        // Escuta mortes globais dos bots
        BOTDeath.OnAnyBotKilled -= HandleAnyBotKilled;
        BOTDeath.OnAnyBotKilled += HandleAnyBotKilled;
    }

    void OnDisable()
    {
        BOTDeath.OnAnyBotKilled -= HandleAnyBotKilled;
    }

    void Start()
    {
        if (!enabled) return;

        // Só o Servidor/Host pode spawnar
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[BotSpawner_Proto] Desativado (só o Host/Server pode spawnar bots).");
            enabled = false;
            return;
        }

        if (botPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[BotSpawner_Proto] Configura botPrefab e spawnPoints.");
            return;
        }

        roundActive = true;
        roundTimer = roundDuration;

        SpawnInitialBots();
    }

    void Update()
    {
        // ADICIONADO: Pára o update se o Netcode não estiver pronto
        if (NetworkManager.Singleton == null) return;

        if (!roundActive || !NetworkManager.Singleton.IsServer) return;

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
        {
            roundActive = false;
            Debug.Log("[BotSpawner_Proto] Ronda terminou. Não há mais respawns.");
        }
    }
    // ------------------------------------------------------------
    // Spawn inicial
    // ------------------------------------------------------------
    void SpawnInitialBots()
    {
        int toSpawn = Mathf.Min(count, maxAliveBots);

        for (int i = 0; i < toSpawn; i++)
        {
            SpawnOne();
        }

        Debug.Log($"[BotSpawner_Proto] Spawn inicial de {toSpawn} bots.");
    }

    // ------------------------------------------------------------
    // Spawn unitário (injeção de waypoints) - MODIFICADO
    // ------------------------------------------------------------
    void SpawnOne()
    {
        if (!roundActive) return;
        if (aliveBots >= maxAliveBots) return;
        if (botPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return; // Segurança extra

        var spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        nextSpawnIndex++;

        // --- MODIFICADO: Usa Instantiate e depois Spawn ---
        var bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);

        spawnedTotal++;
        aliveBots++;
        bot.name = $"Bot_{spawnedTotal}";

        // Dá os waypoints ao BotAI_Proto
        var ai = bot.GetComponent<BotAI_Proto>();
        if (ai != null)
        {
            ai.patrolPoints = patrolWaypoints;
        }

        // Torna o bot "real" na rede
        var netObj = bot.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogError($"[BotSpawner_Proto] botPrefab '{botPrefab.name}' não tem um NetworkObject!");
            Destroy(bot);
            aliveBots--;
        }
    }

    // ------------------------------------------------------------
    // Chamado sempre que QUALQUER bot morre (BOTDeath.OnAnyBotKilled)
    // ------------------------------------------------------------
    void HandleAnyBotKilled()
    {
        if (!roundActive || !NetworkManager.Singleton.IsServer) return;

        // Um bot a menos
        aliveBots = Mathf.Max(0, aliveBots - 1);

        // Regra: matas 1, nascem 2 (se houver espaço e tempo de ronda)
        StartCoroutine(RespawnRoutine());
        StartCoroutine(RespawnRoutine());
    }

    // ------------------------------------------------------------
    // Coroutine de respawn (um bot por chamada)
    // ------------------------------------------------------------
    IEnumerator RespawnRoutine()
    {
        if (!roundActive || !NetworkManager.Singleton.IsServer) yield break;

        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        SpawnOne();
    }

    // ------------------------------------------------------------
    // Mantido só para compatibilidade com BotRespawnLink
    // ------------------------------------------------------------
    public void ScheduleRespawn(Transform[] waypointsFromDead)
    {
        // Intencionalmente vazio.
    }
}