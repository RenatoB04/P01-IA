using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RespawnUIManager : MonoBehaviour
{
    [Header("Referências da UI")]
    [SerializeField] private GameObject respawnPanel;
    [SerializeField] private Button respawnButton;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Configuração")]
    [SerializeField] private float countdownTime = 3.0f;

    private Health localHealth;
    private PlayerDeathAndRespawn localRespawner;
    private Coroutine _uiSequenceCoroutine;

    private void OnEnable()
    {
        Debug.Log("[UI CHECK] O RespawnUIManager começou! À procura do jogador...");

        if (respawnPanel) respawnPanel.SetActive(false);
        else Debug.LogError("[UI ERRO] O 'Respawn Panel' não está arrastado no Inspector!");

        StartCoroutine(FindLocalPlayer());
    }

    private void OnDisable()
    {
        if (localHealth != null)
        {
            localHealth.isDead.OnValueChanged -= OnPlayerDeathChanged;
        }
    }

    private IEnumerator FindLocalPlayer()
    {
        // Espera até encontrar o NetworkManager e o Jogador
        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        Debug.Log("[UI CHECK] Jogador Local ENCONTRADO na rede!");

        var player = NetworkManager.Singleton.LocalClient.PlayerObject;
        localHealth = player.GetComponentInChildren<Health>();
        localRespawner = player.GetComponentInChildren<PlayerDeathAndRespawn>();

        if (localHealth != null)
        {
            Debug.Log($"[UI CHECK] Script de Vida encontrado! Está morto agora? {localHealth.isDead.Value}");

            // Subscreve para saber quando morre
            localHealth.isDead.OnValueChanged += OnPlayerDeathChanged;

            if (localHealth.isDead.Value) ShowDeathScreen();
        }
        else
        {
            Debug.LogError("[UI ERRO] Não encontrei o script 'Health' no jogador!");
        }

        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveAllListeners();
            respawnButton.onClick.AddListener(OnRespawnClicked);
        }
    }

    private void OnPlayerDeathChanged(bool wasDead, bool isDead)
    {
        Debug.Log($"[UI CHECK] O Jogador morreu? {isDead}");
        if (isDead)
            ShowDeathScreen();
        else
            HideDeathScreen();
    }

    private void ShowDeathScreen()
    {
        Debug.Log("[UI CHECK] A LIGAR O CANVAS DE MORTE!");
        if (respawnPanel) respawnPanel.SetActive(true);

        if (respawnButton) respawnButton.gameObject.SetActive(true);
        if (timerText) timerText.gameObject.SetActive(false);
        if (messageText) messageText.gameObject.SetActive(true);

        GameplayCursor.Unlock();
    }

    private void HideDeathScreen()
    {
        Debug.Log("[UI CHECK] A desligar Canvas.");
        if (respawnPanel) respawnPanel.SetActive(false);
        GameplayCursor.Lock();
    }

    private void OnRespawnClicked()
    {
        Debug.Log("[UI CHECK] Botão clicado! Iniciando contagem.");
        if (_uiSequenceCoroutine != null) StopCoroutine(_uiSequenceCoroutine);
        _uiSequenceCoroutine = StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        if (respawnButton) respawnButton.gameObject.SetActive(false);
        if (messageText) messageText.gameObject.SetActive(false);
        if (timerText)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = countdownTime.ToString();
        }

        float timeLeft = countdownTime;
        while (timeLeft > 0)
        {
            if (timerText) timerText.text = Mathf.CeilToInt(timeLeft).ToString();
            yield return null;
            timeLeft -= Time.deltaTime;
        }

        Debug.Log("[UI CHECK] Contagem terminou. Enviando Respawn ao servidor.");
        if (localRespawner != null)
        {
            localRespawner.RespawnServerRpc(true);
        }
    }
}