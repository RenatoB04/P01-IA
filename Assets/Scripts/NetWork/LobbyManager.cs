    using System;
using System.Text;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI (TMP)")]
    [SerializeField] TMP_InputField ifPlayerName;
    [SerializeField] Button btnConnect;
    [SerializeField] Button btnCreate;
    [SerializeField] TMP_Text txtCreatedCode;
    [SerializeField] TMP_InputField ifJoinCode;
    [SerializeField] Button btnJoin;
    [SerializeField] Button btnLeave;
    [SerializeField] TMP_Text txtStatus;
    [SerializeField] Button btnStartGame;            // 👉 botão start original
    [SerializeField] TMP_Text txtCountdown;          // 👉 contador original
    [SerializeField] Button btnPlayBots;             // 👉 NOVO botão "Jogar com Bots"

    [Header("Config")]
    [SerializeField] string gameSceneName = "GameScene";
    [SerializeField] int roomCodeLength = 6;
    [SerializeField] int maxPlayers = 2;
    [SerializeField] int countdownSeconds = 3;

    const string ROOM_PROP_RELAY = "relay"; // joinCode do Unity Relay

    bool _matchStarted = false;
    bool _isCountingDown = false;

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = false;
        Application.runInBackground = true;

        SetUIConnected(false);
        SetUILobbyActions(false);
        if (btnStartGame) btnStartGame.gameObject.SetActive(false);
        if (txtCountdown) txtCountdown.gameObject.SetActive(false);

        Log("Pronto. Define nome e carrega Conectar.");

        btnConnect.onClick.AddListener(OnClickConnect);
        btnCreate.onClick.AddListener(OnClickCreate);
        btnJoin.onClick.AddListener(OnClickJoin);
        btnLeave.onClick.AddListener(OnClickLeave);
        if (btnStartGame) btnStartGame.onClick.AddListener(OnClickStartGame);

        // 👉 regista o novo botão
        if (btnPlayBots) btnPlayBots.onClick.AddListener(OnClickPlayWithBots);
    }

    void OnDestroy()
    {
        btnConnect.onClick.RemoveAllListeners();
        btnCreate.onClick.RemoveAllListeners();
        btnJoin.onClick.RemoveAllListeners();
        btnLeave.onClick.RemoveAllListeners();
        if (btnStartGame) btnStartGame.onClick.RemoveAllListeners();

        // 👉 remove o listener do novo botão
        if (btnPlayBots) btnPlayBots.onClick.RemoveAllListeners();
    }

    void SetUIConnected(bool connected)
    {
        if (btnConnect) btnConnect.interactable = !connected;
        if (ifPlayerName) ifPlayerName.interactable = !connected;

        if (btnCreate) btnCreate.interactable = connected;
        if (btnJoin) btnJoin.interactable = connected && !string.IsNullOrEmpty(ifJoinCode?.text);
        if (ifJoinCode) ifJoinCode.interactable = connected;

        if (btnLeave) btnLeave.interactable = false;
        if (txtCreatedCode) { txtCreatedCode.gameObject.SetActive(false); txtCreatedCode.text = ""; }
        if (btnStartGame) btnStartGame.gameObject.SetActive(false);
    }

    void SetUILobbyActions(bool inRoom)
    {
        if (btnLeave) btnLeave.interactable = inRoom;
        if (btnCreate) btnCreate.interactable = !inRoom && PhotonNetwork.IsConnectedAndReady;
        if (btnJoin) btnJoin.interactable = !inRoom && PhotonNetwork.IsConnectedAndReady && !string.IsNullOrEmpty(ifJoinCode?.text);
        if (ifJoinCode) ifJoinCode.interactable = !inRoom && PhotonNetwork.IsConnectedAndReady;
        if (txtCreatedCode) txtCreatedCode.gameObject.SetActive(inRoom);
    }

    void Log(string msg)
    {
        if (txtStatus) txtStatus.text = msg;
        Debug.Log("[Lobby] " + msg);
    }

    async void OnClickConnect()
    {
        var nick = string.IsNullOrWhiteSpace(ifPlayerName?.text)
            ? ("Player" + UnityEngine.Random.Range(1000, 9999))
            : ifPlayerName.text.Trim();
        PhotonNetwork.NickName = nick;
        Log($"A ligar ao Photon como {PhotonNetwork.NickName}...");

        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();
        else
            Log("Já estás ligado.");

        await EnsureUnityServicesAsync();
    }

    void OnClickCreate()
    {
        string code = GenerateRoomCode(roomCodeLength);
        var options = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 2, 16),
            IsVisible = false,
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { ROOM_PROP_RELAY, "" } },
            CustomRoomPropertiesForLobby = new[] { ROOM_PROP_RELAY }
        };
        Log($"A criar lobby com código {code}...");
        PhotonNetwork.CreateRoom(code, options, TypedLobby.Default);
    }

    void OnClickJoin()
    {
        string code = ifJoinCode?.text?.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) { Log("Escreve um código para entrar."); return; }
        Log($"A entrar no lobby {code}...");
        PhotonNetwork.JoinRoom(code);
    }

    void OnClickLeave()
    {
        if (PhotonNetwork.InRoom)
        {
            Log("A sair do lobby...");
            PhotonNetwork.LeaveRoom();
        }
    }

    // 👉 clique do host no botão “Começar Jogo”
    void OnClickStartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (_isCountingDown || _matchStarted) return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { "startCountdown", true } });
    }

    // -------------------- Photon Callbacks --------------------
    public override void OnConnectedToMaster()
    {
        Log("Ligado ao Master. A entrar no lobby...");
        PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    public override void OnJoinedLobby()
    {
        Log("Estás no lobby. Podes criar ou entrar por código.");
        SetUIConnected(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Log($"Desligado: {cause}");
        SetUIConnected(false);
        SetUILobbyActions(false);
    }

    public override void OnCreatedRoom()
    {
        Log($"Lobby criado. Código: {PhotonNetwork.CurrentRoom?.Name}");
    }

    public override async void OnJoinedRoom()
    {
        string code = PhotonNetwork.CurrentRoom.Name;
        Log($"Entraste no lobby ({code}). Espera o início do jogo ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");

        if (txtCreatedCode)
        {
            txtCreatedCode.gameObject.SetActive(true);
            txtCreatedCode.text = $"Código: {code}";
        }
        SetUILobbyActions(true);

        // Ativa botão só para o host
        if (PhotonNetwork.IsMasterClient && btnStartGame)
            btnStartGame.gameObject.SetActive(true);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Log($"Entrou: {newPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Log($"{otherPlayer.NickName} saiu. ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
    }

    public override async void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("startCountdown"))
        {
            await StartCountdownAndLaunch();
            return;
        }

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_RELAY))
        {
            string joinCode = propertiesThatChanged[ROOM_PROP_RELAY] as string;
            if (!string.IsNullOrEmpty(joinCode) && !IsNgoConnected())
            {
                Log($"Código Relay recebido: {joinCode}. A ligar ao jogo...");
                await StartClientWithRelayAsync(joinCode);
            }
        }
    }

    // ---------------- Countdown ----------------
    async Task StartCountdownAndLaunch()
    {
        if (_isCountingDown) return;
        _isCountingDown = true;

        if (btnStartGame) btnStartGame.interactable = false;
        if (txtCountdown) txtCountdown.gameObject.SetActive(true);

        for (int i = countdownSeconds; i > 0; i--)
        {
            if (txtCountdown) txtCountdown.text = $"Começa em {i}...";
            await Task.Delay(1000);
        }

        if (txtCountdown) txtCountdown.text = "A começar!";

        if (PhotonNetwork.IsMasterClient)
        {
            _matchStarted = true;
            await StartHostWithRelayAndLoadAsync();
        }
    }

    // ---------------- Fluxo híbrido ----------------
    async Task StartHostWithRelayAndLoadAsync()
    {
        await EnsureUnityServicesAsync();
        int maxConnections = Mathf.Max(1, maxPlayers - 1);
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
        Log($"Relay criado. JoinCode: {joinCode}");

        var props = new Hashtable { { ROOM_PROP_RELAY, joinCode } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        var serverData = AllocationUtils.ToRelayServerData(alloc, "dtls");
        transport.SetRelayServerData(serverData);

        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("Falha ao iniciar Host NGO.");
                return;
            }
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    async Task StartClientWithRelayAsync(string joinCode)
    {
        await EnsureUnityServicesAsync();
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var serverData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");
        transport.SetRelayServerData(serverData);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.StartClient();
    }

    bool IsNgoConnected()
    {
        var nm = NetworkManager.Singleton;
        return nm && (nm.IsClient || nm.IsServer);
    }

    async Task EnsureUnityServicesAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = new System.Random();
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++) sb.Append(chars[rnd.Next(chars.Length)]);
        return sb.ToString();
    }

    // -------------------- NOVO: modo offline com bots --------------------
    public void OnClickPlayWithBots()
    {
        Debug.Log("[Lobby] Modo offline com bots (host local).");

        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Lobby] NetworkManager.Singleton == null. Não consigo iniciar host local.");
            return;
        }

        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            bool ok = NetworkManager.Singleton.StartHost();
            if (!ok)
            {
                Debug.LogError("[Lobby] Falha ao iniciar Host local.");
                return;
            }
        }

        // marca que vamos arrancar em modo offline
        PlayerPrefs.SetInt("OfflineMode", 1);

        NetworkManager.Singleton.SceneManager.LoadScene("Prototype", LoadSceneMode.Single);
    }
}
