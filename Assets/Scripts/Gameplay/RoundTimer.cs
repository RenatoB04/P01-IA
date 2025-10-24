using UnityEngine;
using TMPro;

public class RoundTimer : MonoBehaviour
{
    [Header("Tempo da Ronda")]
    public float roundSeconds = 60f;

    [Header("UI")]
    [SerializeField] TMP_Text timerText;       // arrasta o texto do timer (TMP)
    [SerializeField] GameObject deathPanel;    // o MESMO painel de respawn da morte

    float timeLeft;
    bool running;

    void Start()
    {
        StartRound();
    }

    void Update()
    {
        if (!running) return;

        // CRÍTICO: Usar Time.unscaledDeltaTime para ignorar Time.timeScale
        timeLeft -= Time.unscaledDeltaTime; 
        if (timeLeft < 0f) timeLeft = 0f;

        UpdateTimerUI(timeLeft);

        if (timeLeft <= 0f)
            EndRound();
    }

    public void StartRound()
    {
        // CORREÇÃO: Remove Time.timeScale = 1f;
        running = true;
        timeLeft = roundSeconds;
        UpdateTimerUI(timeLeft);

        if (deathPanel) deathPanel.SetActive(false);

        // Opcional: limpar score no início
        if (ScoreManager.Instance) ScoreManager.Instance.ResetScore();
    }

    public void EndRound()
    {
        running = false;
        // CORREÇÃO: Remove Time.timeScale = 0f; (A PAUSA DEVE SER FEITA PELO PlayerDeathAndRespawn.cs)

        if (deathPanel) deathPanel.SetActive(true);
    }

    void UpdateTimerUI(float seconds)
    {
        if (!timerText) return;
        int s = Mathf.CeilToInt(seconds);
        int mm = s / 60;
        int ss = s % 60;
        timerText.text = $"{mm:00}:{ss:00}";
    }
}