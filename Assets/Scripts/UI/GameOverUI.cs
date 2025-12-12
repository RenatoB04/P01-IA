using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("Painel Principal")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button leaveButton;

    [Header("Resultados")]
    [SerializeField] private TextMeshProUGUI statusText; 
    [SerializeField] private TextMeshProUGUI winnerText; 
    [SerializeField] private TextMeshProUGUI scoreText;  

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panel) panel.SetActive(false);
        if (leaveButton) leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    public void ShowGameOver(string message, string winnerName, int finalScore)
    {
        if (panel) 
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling(); 
        }
        
        if (statusText) statusText.text = message;
        
        if (winnerText) 
        {
            winnerText.text = string.IsNullOrEmpty(winnerName) 
                ? "Sem Vencedor" 
                : $"Vencedor: {winnerName}";
        }

        if (scoreText)
        {
            scoreText.text = $"Pontuação: {finalScore}";
        }

        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnLeaveClicked()
    {
        if (NetworkManager.Singleton) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Lobby");
    }
}