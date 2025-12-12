using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] TMP_Text scoreText;

    [Header("Valores")]
    public int pointsPerKill = 10;

    public int Score { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        
        UpdateUI();
    }

    void OnEnable()
    {
        BOTDeath.OnAnyBotKilled += AddKillPoints;
    }

    void OnDisable()
    {
        BOTDeath.OnAnyBotKilled -= AddKillPoints;
    }

    void AddKillPoints()
    {
        Score += pointsPerKill;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = $"Score: {Score}";
    }

    public void ResetScore()
    {
        Score = 0;
        UpdateUI();
    }
}