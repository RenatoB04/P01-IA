using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Chat;
using ExitGames.Client.Photon;
using Photon.Pun; 

public class SimpleLobbyChat : MonoBehaviour, IChatClientListener
{
    [Header("Photon Chat")]
    [Tooltip("Se vazio, tentamos usar PhotonServerSettings.AppSettings.AppIdChat como fallback.")]
    [SerializeField] private string appIdChat = "";
    [SerializeField] private string chatVersion = "1.0";
    [SerializeField] private string fixedRegion = "eu";
    [SerializeField] private string lobbyChannel = "global-lobby";

    [Header("Identidade")]
    [SerializeField] private TMP_InputField playerNameInput;   
    [SerializeField] private string invalidSampleName = "Nome Jogador";

    [Header("UI (mensagens)")]
    [SerializeField] private TMP_InputField inputField;   
    [SerializeField] private Button sendButton;           
    [SerializeField] private ScrollRect scrollRect;       
    [SerializeField] private TMP_Text messagesText;       

    [Header("Controlo de fluxo")]
    [SerializeField] private Button connectButton;        
    [SerializeField] private bool sendOnEnter = true;     
    [SerializeField] private int maxVisibleMessages = 100;

    [Header("Debug / Test")]
    [Tooltip("Se true, tenta auto-conectar 1s após Start() (útil para testes no Editor).")]
    public bool autoConnectForTesting = false;

    private ChatClient _chat;
    private readonly Queue<string> _lines = new Queue<string>(128);
    private bool isSubscribed = false;
    private bool isConnectingOrConnected = false;
    private string displayName = "";

    
    void Awake()
    {
        SetChatInteractable(false);

        if (sendButton) sendButton.onClick.AddListener(OnClickSend);
        if (inputField && sendOnEnter) inputField.onSubmit.AddListener(_ => OnClickSend());

        if (connectButton) connectButton.onClick.AddListener(OnConnectButtonPressed);

        TryAutoWireUI();
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;

        
        if (string.IsNullOrWhiteSpace(appIdChat))
        {
            var settings = PhotonNetwork.PhotonServerSettings;
            if (settings != null && settings.AppSettings != null && !string.IsNullOrWhiteSpace(settings.AppSettings.AppIdChat))
            {
                appIdChat = settings.AppSettings.AppIdChat;
                Debug.Log("[SimpleLobbyChat] AppIdChat vazio no componente; a usar AppId do PhotonServerSettings.");
            }
        }

        if (string.IsNullOrWhiteSpace(appIdChat))
        {
            Debug.LogError("[SimpleLobbyChat] AppIdChat vazio. Define no componente ou em PhotonServerSettings > AppSettings > AppIdChat.");
            AppendSystem("[chat] AppIdChat vazio. Preenche AppIdChat no componente ou em PhotonServerSettings.");
            enabled = false;
            yield break;
        }

        AppendSystem("Define o teu nome e carrega <b>Conectar</b> para ativar o chat.");

        
        if (autoConnectForTesting)
        {
            yield return new WaitForSeconds(1f);
            AppendSystem("[chat] autoConnectForTesting activo -> a tentar conectar...");
            OnConnectButtonPressed();
        }
    }

    void Update()
    {
        
        try
        {
            _chat?.Service();
        }
        catch (Exception ex)
        {
            Debug.LogError("[SimpleLobbyChat] Exception no ChatClient.Service(): " + ex);
        }
    }

    void OnDestroy()
    {
        if (_chat != null) { _chat.Disconnect(); _chat = null; }
        if (connectButton) connectButton.onClick.RemoveListener(OnConnectButtonPressed);
        if (sendButton) sendButton.onClick.RemoveListener(OnClickSend);
        if (inputField && sendOnEnter) inputField.onSubmit.RemoveAllListeners();
    }

    
    public void OnConnectButtonPressed()
    {
        if (isConnectingOrConnected)
        {
            AppendSystem("[chat] Já ligado ou a tentar ligar.");
            Debug.Log("[SimpleLobbyChat] OnConnectButtonPressed: já ligado/ouligando.");
            return;
        }

        string proposed = GetProposedName();
        if (!IsNameValid(proposed))
        {
            AppendSystem("⚠ Define um <b>nome válido</b> antes de ligar o chat.");
            SetChatInteractable(false);
            Debug.Log("[SimpleLobbyChat] Nome inválido para chat: '" + proposed + "'");
            return;
        }

        displayName = proposed.Trim();
        AppendSystem($"[chat] A tentar conectar como <b>{displayName}</b>...");

        
        try
        {
            if (_chat == null)
            {
                _chat = new ChatClient(this);
                _chat.ChatRegion = fixedRegion;
                Debug.Log("[SimpleLobbyChat] Criado ChatClient, region=" + fixedRegion);
            }

            bool ok = _chat.Connect(appIdChat, chatVersion, new AuthenticationValues(displayName));
            isConnectingOrConnected = true;
            Debug.Log($"[SimpleLobbyChat] Connect chamada -> returned {ok}. appIdChat length={(appIdChat?.Length ?? 0)}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[SimpleLobbyChat] Exceção ao chamar Connect(): " + ex);
            AppendSystem("[chat] Erro ao iniciar ligação: " + ex.Message);
            isConnectingOrConnected = false;
        }
    }

    string GetProposedName()
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            return playerNameInput.text;

        if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            return PhotonNetwork.NickName;

        return string.Empty;
    }

    bool IsNameValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string n = name.Trim();
        if (n.Length < 2) return false;
        if (!string.IsNullOrWhiteSpace(invalidSampleName) &&
            n.Equals(invalidSampleName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    void SetChatInteractable(bool enabledUI)
    {
        if (sendButton) sendButton.interactable = enabledUI;
        if (inputField) inputField.readOnly = !enabledUI;
    }

    
    public void OnClickSend()
    {
        if (inputField == null) { AppendSystem("⚠ InputField da mensagem não está atribuído."); return; }

        var currentName = GetProposedName();
        if (!IsNameValid(displayName) || !IsNameValid(currentName))
        {
            AppendSystem("⚠ Define o teu <b>nome</b> e carrega <b>Conectar</b>.");
            SetChatInteractable(false);
            return;
        }
        displayName = currentName.Trim();

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
            if (scrollRect != null && scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(messagesText.rectTransform);
                float newHeight = messagesText.preferredHeight + 20f;
                var size = scrollRect.content.sizeDelta;
                scrollRect.content.sizeDelta = new Vector2(size.x, newHeight);
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

    
    public void OnConnected()
    {
        Debug.Log("[SimpleLobbyChat] OnConnected()");
        AppendSystem("Connected. Subscribing channel...");
        isSubscribed = false;
        try
        {
            _chat?.Subscribe(new[] { lobbyChannel }, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[SimpleLobbyChat] Ex on Subscribe: " + ex);
            AppendSystem("[chat] Erro ao subscrever canal: " + ex.Message);
        }
    }

    public void OnSubscribed(string[] channels, bool[] results)
    {
        Debug.Log("[SimpleLobbyChat] OnSubscribed(): channels=" + string.Join(",", channels));
        isSubscribed = true;
        AppendSystem($"Joined channel <b>{lobbyChannel}</b>.");
        SetChatInteractable(true);
    }

    public void OnUnsubscribed(string[] channels)
    {
        Debug.Log("[SimpleLobbyChat] OnUnsubscribed()");
        isSubscribed = false;
        SetChatInteractable(false);
        AppendSystem($"Left channel(s): {string.Join(", ", channels)}.");
    }

    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        Debug.Log($"[SimpleLobbyChat] OnGetMessages: channel={channelName} count={messages.Length}");
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
        Debug.Log("[SimpleLobbyChat] OnDisconnected()");
        isSubscribed = false;
        isConnectingOrConnected = false;
        SetChatInteractable(false);
        AppendSystem("<color=#f66>Disconnected.</color>");
    }

    public void OnChatStateChange(ChatState state)
    {
        Debug.Log("[SimpleLobbyChat] OnChatStateChange: " + state);
        AppendSystem($"[chat] Chat state: {state}");
    }

    public void DebugReturn(DebugLevel level, string message)
    {
        Debug.Log($"[SimpleLobbyChat] DebugReturn ({level}): {message}");
        AppendSystem($"[chat] Debug: {message}");
    }

    
    public void OnPrivateMessage(string sender, object message, string channelName) { }
    public void OnUserSubscribed(string channel, string user) { }
    public void OnUserUnsubscribed(string channel, string user) { }
    public void OnStatusUpdate(string user, int status, bool gotMessage, object message) { }
}