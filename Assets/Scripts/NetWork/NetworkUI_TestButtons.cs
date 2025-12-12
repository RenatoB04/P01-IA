using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkUI_TestButtons : MonoBehaviour
{
    [Header("Botões")]
    public Button hostButton;
    public Button clientButton;
    public Button serverButton;

    [Header("Prefab do Jogador")]
    [Tooltip("Arrasta o teu Prefab do Jogador (com NetworkObject no root).")]
    public GameObject playerPrefabToSpawn;

    void Start()
    {
        
        if (hostButton)
        {
            hostButton.onClick.RemoveListener(StartHost);
            hostButton.onClick.AddListener(StartHost);
        }
        if (clientButton)
        {
            clientButton.onClick.RemoveListener(StartClient);
            clientButton.onClick.AddListener(StartClient);
        }
        if (serverButton)
        {
            serverButton.onClick.RemoveListener(StartServer);
            serverButton.onClick.AddListener(StartServer);
        }

        if (NetworkManager.Singleton != null)
        {
            
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton é nulo na cena.");
        }
    }

    private void StartHost()
    {
        if (NetworkManager.Singleton == null) { Debug.LogError("Sem NetworkManager."); return; }

        Debug.Log("Starting Host...");
        bool ok = NetworkManager.Singleton.StartHost();
        if (ok) HideButtons();
        else Debug.LogError("Falha ao iniciar Host (porta em uso?).");
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton == null) { Debug.LogError("Sem NetworkManager."); return; }

        Debug.Log("Starting Client...");
        bool ok = NetworkManager.Singleton.StartClient();
        if (ok) HideButtons();
        else Debug.LogError("Falha ao iniciar Client.");
    }

    private void StartServer()
    {
        if (NetworkManager.Singleton == null) { Debug.LogError("Sem NetworkManager."); return; }

        Debug.Log("Starting Server...");
        bool ok = NetworkManager.Singleton.StartServer();
        if (ok) HideButtons();
        else Debug.LogError("Falha ao iniciar Server.");
    }

    private void HideButtons()
    {
        if (hostButton) hostButton.gameObject.SetActive(false);
        if (clientButton) clientButton.gameObject.SetActive(false);
        if (serverButton) serverButton.gameObject.SetActive(false);
    }

    
    private void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        
        if (nm.ConnectedClients.TryGetValue(clientId, out var client) &&
            client != null && client.PlayerObject != null)
        {
            Debug.Log($"[Spawn Skip] Client {clientId} já tem PlayerObject (auto-spawn ou outro script).");
            return;
        }

        
        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefabToSpawn == null)
        {
            Debug.LogError("Player Prefab To Spawn não definido no Inspector!");
            return;
        }

        var instance = Instantiate(playerPrefabToSpawn);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("O prefab do jogador NÃO tem NetworkObject no root!");
            Destroy(instance);
            return;
        }

        
        netObj.SpawnAsPlayerObject(clientId, true);
        Debug.Log($"Jogador spawnado para Client {clientId}");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}