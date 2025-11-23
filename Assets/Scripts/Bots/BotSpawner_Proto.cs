using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class BotSpawner_Proto : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("O Prefab do Bot (TEM de estar na lista NetworkPrefabs do NetworkManager!).")]
    public GameObject botPrefab;
    
    [Tooltip("Pontos onde os bots podem nascer.")]
    public Transform[] spawnPoints;

    [Tooltip("Caminho de patrulha para os bots.")]
    public Transform[] patrolWaypoints;

    [Header("Regras da Horda")]
    public int initialBotCount = 2;
    public int maxAliveBots = 5;
    public float respawnDelay = 3f;
    
    [Header("Multiplayer")]
    [Tooltip("Se false, os bots só aparecem no modo Offline. Se true, aparecem também no Multiplayer.")]
    public bool enableInMultiplayer = true;

    [Header("Debug")]
    public bool forceSpawnInEditor = true;

    private int currentAliveBots = 0;
    private bool isSpawningActive = false;

    // Ciclo de Vida
    void Awake()
    {
        // Se NÃO forçarmos no editor E a flag "OfflineMode" não estiver ativa, desliga-se.
        if (!forceSpawnInEditor && PlayerPrefs.GetInt("OfflineMode", 0) != 1)
        {
            // Mas se estivermos num build multiplayer, queremos que continue a tentar
            // Por isso não desativamos já, deixamos o Start decidir com o Netcode.
        }
    }

    void Start()
    {
        StartCoroutine(WaitForServer());
    }

    IEnumerator WaitForServer()
    {
        // Espera até o Netcode arrancar
        yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        // Só o Servidor (Host Offline ou Host Online) pode spawnar
        if (NetworkManager.Singleton.IsServer)
        {
            // Verificação extra para não spawnar em MP se não quisermos
            bool isOfflineMode = PlayerPrefs.GetInt("OfflineMode", 0) == 1;
            
            if (!isOfflineMode && !enableInMultiplayer && !forceSpawnInEditor)
            {
                Debug.Log("[BotSpawner] Desativado (Modo Online e enableInMultiplayer=false).");
                enabled = false;
                yield break;
            }

            Debug.Log("[BotSpawner] SOU O HOST. A iniciar ronda de bots...");
            isSpawningActive = true;
            
            // Subscrever mortes
            BOTDeath.OnAnyBotKilled += HandleBotDeath;

            // Spawn Inicial
            for (int i = 0; i < initialBotCount; i++)
            {
                SpawnBot();
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            // Sou Cliente, fico quieto
            enabled = false;
        }
    }

    void OnDestroy()
    {
        BOTDeath.OnAnyBotKilled -= HandleBotDeath;
    }

    // --- Lógica HIDRA: Matas 1, Nascem 2 ---
    void HandleBotDeath()
    {
        if (!isSpawningActive) return;

        currentAliveBots--;
        if (currentAliveBots < 0) currentAliveBots = 0;

        StartCoroutine(SpawnRoutine(2));
    }

    IEnumerator SpawnRoutine(int amount)
    {
        yield return new WaitForSeconds(respawnDelay);

        for (int i = 0; i < amount; i++)
        {
            if (currentAliveBots < maxAliveBots)
            {
                SpawnBot();
                yield return new WaitForSeconds(1f);
            }
        }
    }

    void SpawnBot()
    {
        if (botPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        if (!NetworkManager.Singleton.IsServer) return;

        // Escolhe posição aleatória
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        
        // Instancia
        GameObject bot = Instantiate(botPrefab, sp.position, sp.rotation);
        
        // Configura IA
        var ai = bot.GetComponent<BotAI_Proto>();
        if (ai != null) ai.patrolPoints = patrolWaypoints;

        // SPAWN NA REDE
        var netObj = bot.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            currentAliveBots++;
        }
        else
        {
            Debug.LogError("[BotSpawner] O Bot Prefab não tem NetworkObject!");
            Destroy(bot);
        }
    }
    
    // Compatibilidade com scripts antigos
    public void ScheduleRespawn(Transform[] t) { }
}