using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class BotSpawner_Proto : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("O Prefab do Bot (TEM de estar na lista NetworkPrefabs do NetworkManager!).")]
    public GameObject botPrefab; // Prefab do bot que será instanciado

    [Tooltip("Pontos onde os bots podem nascer.")]
    public Transform[] spawnPoints; // Lista de pontos de spawn possíveis

    [Tooltip("Caminho de patrulha para os bots.")]
    public Transform[] patrolWaypoints; // Waypoints de patrulha que os bots irão usar

    [Header("Regras da Horda")]
    public int initialBotCount = 2; // Quantidade inicial de bots na cena
    public int maxAliveBots = 5;    // Máximo de bots ativos ao mesmo tempo
    public float respawnDelay = 3f; // Atraso entre morte e respawn de bots

    [Header("Multiplayer")]
    [Tooltip("Se false, os bots só aparecem no modo Offline. Se true, aparecem também no Multiplayer.")]
    public bool enableInMultiplayer = true;

    [Header("Debug")]
    public bool forceSpawnInEditor = true; // Se true, força spawn mesmo fora do servidor

    private int currentAliveBots = 0;      // Contador de bots vivos
    private bool isSpawningActive = false; // Estado de atividade do spawner

    void Awake()
    {
        // Aqui poderias adicionar lógica extra para desativar spawn em certas condições
        if (!forceSpawnInEditor && PlayerPrefs.GetInt("OfflineMode", 0) != 1)
        {
            // Exemplo: desativar spawner se não estiver em modo offline e não for editor
        }
    }

    void Start()
    {
        // Espera até o NetworkManager estar pronto antes de iniciar o spawn
        StartCoroutine(WaitForServer());
    }

    IEnumerator WaitForServer()
    {
        // Aguarda até o NetworkManager estar inicializado e a escutar
        yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        // Apenas o servidor/host realiza spawn
        if (NetworkManager.Singleton.IsServer)
        {
            bool isOfflineMode = PlayerPrefs.GetInt("OfflineMode", 0) == 1;

            // Condições para desativar o spawner em multiplayer
            if (!isOfflineMode && !enableInMultiplayer && !forceSpawnInEditor)
            {
                Debug.Log("[BotSpawner] Desativado (Modo Online e enableInMultiplayer=false).");
                enabled = false;
                yield break;
            }

            Debug.Log("[BotSpawner] SOU O HOST. A iniciar ronda de bots...");
            isSpawningActive = true;

            // Regista evento global de morte de bots
            BOTDeath.OnAnyBotKilled += HandleBotDeath;

            // Spawn inicial dos bots
            for (int i = 0; i < initialBotCount; i++)
            {
                SpawnBot();
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            // Se não for servidor, desativa o script
            enabled = false;
        }
    }

    void OnDestroy()
    {
        // Remove listener para evitar chamadas após destruição do spawner
        BOTDeath.OnAnyBotKilled -= HandleBotDeath;
    }

    // Chamado quando um bot morre
    void HandleBotDeath()
    {
        if (!isSpawningActive) return;

        currentAliveBots--;
        if (currentAliveBots < 0) currentAliveBots = 0;

        // Inicia rotina de respawn
        StartCoroutine(SpawnRoutine(2));
    }

    IEnumerator SpawnRoutine(int amount)
    {
        // Espera o tempo definido antes de respawn
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

        // Escolhe ponto de spawn aleatório
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Instancia o bot
        GameObject bot = Instantiate(botPrefab, sp.position, sp.rotation);

        // Configura waypoints de patrulha
        var ai = bot.GetComponent<BotAI_Proto>();
        if (ai != null) ai.patrolPoints = patrolWaypoints;

        // Verifica NetworkObject e spawna em rede
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

    // Placeholder para compatibilidade com BotRespawnLink
    public void ScheduleRespawn(Transform[] t) { }
}
