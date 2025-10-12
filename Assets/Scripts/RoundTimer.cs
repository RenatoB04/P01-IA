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

        timeLeft -= Time.unscaledDeltaTime; // mostra tempo mesmo se pausares por engano
        if (timeLeft < 0f) timeLeft = 0f;

        UpdateTimerUI(timeLeft);

        if (timeLeft <= 0f)
            EndRound();
    }

    public void StartRound()
    {
        Time.timeScale = 1f;   // garante jogo a correr
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
        Time.timeScale = 0f;   // pausa jogo inteiro

        if (deathPanel) deathPanel.SetActive(true);
        // No botão "Respawn" do painel, adiciona também RoundTimer.StartRound()
        // (além do que já faz para respawn quando morres)
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
