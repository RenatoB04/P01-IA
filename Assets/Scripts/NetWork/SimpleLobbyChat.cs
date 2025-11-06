using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Chat;
using ExitGames.Client.Photon;
// Opcional se quiseres sincronizar também o NickName do PUN:
// using Photon.Pun;

public class SimpleLobbyChat : MonoBehaviour, IChatClientListener
{
    [Header("Photon Chat")]
    [SerializeField] private string appIdChat = "";
    [SerializeField] private string chatVersion = "1.0";
    [SerializeField] private string fixedRegion = "eu";
    [SerializeField] private string lobbyChannel = "global-lobby";

    [Header("Identidade")]
    [SerializeField] private TMP_InputField playerNameInput;   // input do NOME (do teu Lobby Manager)
    [SerializeField] private string invalidSampleName = "Nome Jogador";

    [Header("UI (mensagens)")]
    [SerializeField] private TMP_InputField inputField;   // onde escreves a MENSAGEM
    [SerializeField] private Button sendButton;           // botão Enviar
    [SerializeField] private ScrollRect scrollRect;       // ScrollView
    [SerializeField] private TMP_Text messagesText;       // TMP_Text no Content

    [Header("Controlo de fluxo")]
    [SerializeField] private Button connectButton;        // (opcional) botão "Conectar" do lobby
    [SerializeField] private bool sendOnEnter = true;     // Enter envia (bloqueado até subscrever)
    [SerializeField] private int maxVisibleMessages = 100;

    private ChatClient _chat;
    private readonly Queue<string> _lines = new Queue<string>(128);
    private bool isSubscribed = false;
    private bool isConnectingOrConnected = false;
    private string displayName = "";

    // ================== Unity ==================
    void Awake()
    {
        // Liga/desliga UI do chat até haver ligação
        SetChatInteractable(false);

        if (sendButton) sendButton.onClick.AddListener(OnClickSend);
        if (inputField && sendOnEnter) inputField.onSubmit.AddListener(_ => OnClickSend());

        if (connectButton) connectButton.onClick.AddListener(OnConnectButtonPressed);

        TryAutoWireUI();
    }

    void Start()
    {
        Application.runInBackground = true;

        if (string.IsNullOrWhiteSpace(appIdChat))
        {
            Debug.LogError("[SimpleLobbyChat] AppIdChat vazio. Cria App (Chat) no Photon Dashboard e cola aqui.");
            enabled = false;
            return;
        }

        // IMPORTANTE: NÃO ligar automaticamente aqui.
        // Esperamos explicitamente pelo clique no botão "Conectar".
        AppendSystem("Define o teu nome e carrega <b>Conectar</b> para ativar o chat.");
    }

    void Update() => _chat?.Service();

    void OnDestroy()
    {
        if (_chat != null) { _chat.Disconnect(); _chat = null; }
        if (connectButton) connectButton.onClick.RemoveListener(OnConnectButtonPressed);
        if (sendButton) sendButton.onClick.RemoveListener(OnClickSend);
        if (inputField && sendOnEnter) inputField.onSubmit.RemoveAllListeners();
    }

    // ================== Fluxo de ligação ==================
    public void OnConnectButtonPressed()
    {
        // Ignora se já existir ligação a decorrer/feita
        if (isConnectingOrConnected) { AppendSystem("Já ligado ou a ligar…"); return; }

        // Validar nome
        string proposed = GetProposedName();
        if (!IsNameValid(proposed))
        {
            AppendSystem("⚠ Define um <b>nome válido</b> antes de ligar o chat.");
            SetChatInteractable(false);
            return;
        }

        displayName = proposed.Trim();

        // (Opcional) sincronizar também com o PUN:
        // PhotonNetwork.NickName = displayName;

        // Ligar ao Photon Chat
        _chat = new ChatClient(this);
        _chat.ChatRegion = fixedRegion;
        _chat.Connect(appIdChat, chatVersion, new AuthenticationValues(displayName));
        isConnectingOrConnected = true;

        AppendSystem($"Connecting as <b>{displayName}</b> to <i>{fixedRegion}</i>…");
    }

    string GetProposedName()
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            return playerNameInput.text;

        // (Opcional) se quiseres aceitar o NickName do PUN quando o input estiver vazio:
        // if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        //     return PhotonNetwork.NickName;

        return string.Empty;
    }

    bool IsNameValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string n = name.Trim();
        if (n.Length < 2) return false;
        if (!string.IsNullOrWhiteSpace(invalidSampleName) &&
            n.Equals(invalidSampleName, System.StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    void SetChatInteractable(bool enabled)
    {
        if (sendButton) sendButton.interactable = enabled;
        if (inputField) inputField.readOnly = !enabled;
    }

    // ================== UI de envio ==================
    public void OnClickSend()
    {
        if (inputField == null) { AppendSystem("⚠ InputField da mensagem não está atribuído."); return; }

        if (!IsNameValid(displayName) || !IsNameValid(GetProposedName()))
        {
            AppendSystem("⚠ Define o teu <b>nome</b> e carrega <b>Conectar</b>.");
            SetChatInteractable(false);
            return;
        }

        if (_chat == null || !_chat.CanChat || !isSubscribed)
        {
            AppendSystem("⚠ O chat ainda não está pronto. Tenta novamente.");
            SetChatInteractable(false);
            return;
        }

        string msg = inputField.text;
        if (string.IsNullOrWhiteSpace(msg)) return;

        string trimmed = msg.Trim();
        bool sent = _chat.PublishMessage(lobbyChannel, trimmed);
        Debug.Log($"[SimpleLobbyChat] PublishMessage returned: {sent}");

        // Eco local imediato
        AppendLine($"<b>{displayName}</b>: {trimmed}");

        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    private void AppendLine(string line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > maxVisibleMessages) _lines.Dequeue();

        if (messagesText != null)
        {
            messagesText.enableWordWrapping = true;
            messagesText.richText = true;
            messagesText.alignment = TextAlignmentOptions.TopLeft;
            messagesText.text = string.Join("\n", _lines);

            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                // Ajusta altura e faz auto-scroll para o fundo
                LayoutRebuilder.ForceRebuildLayoutImmediate(messagesText.rectTransform);
                float newHeight = messagesText.preferredHeight + 20f;
                scrollRect.content.sizeDelta = new Vector2(scrollRect.content.sizeDelta.x, newHeight);
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    private void AppendSystem(string text) => AppendLine($"<color=#888>[system]</color> {text}");

    void TryAutoWireUI()
    {
        if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (messagesText == null && scrollRect != null)
            messagesText = scrollRect.content != null ? scrollRect.content.GetComponentInChildren<TMP_Text>(true) : null;
    }

    // ================== Photon Chat callbacks ==================
    public void OnConnected()
    {
        AppendSystem("Connected. Subscribing channel...");
        isSubscribed = false;
        _chat.Subscribe(new[] { lobbyChannel }, 0);
    }

    public void OnSubscribed(string[] channels, bool[] results)
    {
        isSubscribed = true;
        AppendSystem($"Joined channel <b>{lobbyChannel}</b>.");
        SetChatInteractable(true); // só agora liberta Enter + Enviar
    }

    public void OnUnsubscribed(string[] channels)
    {
        isSubscribed = false;
        SetChatInteractable(false);
        AppendSystem($"Left channel(s): {string.Join(", ", channels)}.");
    }

    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        for (int i = 0; i < messages.Length; i++)
        {
            string sender = senders[i];
            string text = messages[i]?.ToString() ?? "";
            if (!string.Equals(sender, displayName))
                AppendLine($"<b>{sender}</b>: {text}");
        }
    }

    public void OnDisconnected()
    {
        isSubscribed = false;
        isConnectingOrConnected = false;
        SetChatInteractable(false);
        AppendSystem("<color=#f66>Disconnected.</color>");
    }

    public void OnChatStateChange(ChatState state) { }

    // Assinatura usada nas versões antigas do Photon Chat (compatível)
    public void OnPrivateMessage(string sender, object message, string channelName) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
    public void DebugReturn(DebugLevel level, string message) { }
}
