using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Photon.Pun;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class PauseMenuManager : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("UI")]
    public GameObject pausePanel;
    public Button btnResume;
    public Button btnDisconnect;
    public Button btnQuit;
    public TMP_Text txtStatus;

    bool isMenuOpen = false;
    PlayerInput localInput;

    void Start()
    {
        
        IsPaused   = false;
        isMenuOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (pausePanel) pausePanel.SetActive(false);

        if (btnResume)     btnResume.onClick.AddListener(OnClickResume);
        if (btnDisconnect) btnDisconnect.onClick.AddListener(OnClickDisconnect);
        if (btnQuit)       btnQuit.onClick.AddListener(OnClickQuit);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped           += OnServerStopped;
        }

        EnsureEventSystem();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleMenu();
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        IsPaused   = isMenuOpen;

        if (pausePanel) pausePanel.SetActive(isMenuOpen);

        Cursor.lockState = isMenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = isMenuOpen;

        
        if (localInput == null)
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer)
                localInput = localPlayer.GetComponentInChildren<PlayerInput>();
        }

        
        if (localInput)
            localInput.enabled = !isMenuOpen;

        
        if (isMenuOpen && btnResume && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(btnResume.gameObject);
    }

    GameObject FindLocalPlayer()
    {
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
                return player;
        }
        return null;
    }

    void OnClickResume()     => ToggleMenu();
    void OnClickQuit()       => Application.Quit();
    void OnClickDisconnect() => StartCoroutine(DisconnectAndReturnToLobby());

    System.Collections.IEnumerator DisconnectAndReturnToLobby()
    {
        if (txtStatus) txtStatus.text = "A desconectar...";

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        yield return new WaitForSeconds(0.5f);

        if (txtStatus) txtStatus.text = "A voltar ao menu...";
        SceneManager.LoadScene("Lobby");
    }

    void OnClientDisconnected(ulong clientId)
    {
        
        if (!NetworkManager.Singleton.IsServer && clientId == 0)
        {
            Debug.Log("[PauseMenu] Host caiu. A voltar ao lobby...");
            SceneManager.LoadScene("Lobby");
        }
    }

    void OnServerStopped(bool _)
    {
        Debug.Log("[PauseMenu] Servidor parado. A voltar ao lobby...");
        SceneManager.LoadScene("Lobby");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped           -= OnServerStopped;
        }

        if (btnResume)     btnResume.onClick.RemoveAllListeners();
        if (btnDisconnect) btnDisconnect.onClick.RemoveAllListeners();
        if (btnQuit)       btnQuit.onClick.RemoveAllListeners();
    }

    
    void EnsureEventSystem()
    {
        var es = FindObjectOfType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        else if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
            var old = es.GetComponent<StandaloneInputModule>();
            if (old) Destroy(old);
        }
    }
}