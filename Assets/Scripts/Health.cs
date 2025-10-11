using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class Health : MonoBehaviour
{
    [Header("Config")]
    public float maxHealth = 100f;
    public int team = -1; // -1 = neutro; 0 = jogador; 1 = bots

    [Header("Runtime")]
    public float currentHealth;
    public bool isDead;

    [Header("Events")]
    public UnityEvent<float, float> OnHealthChanged; 
    public UnityEvent OnDied;

    [Header("UI (Opcional)")]
    public TextMeshProUGUI healthText; // texto hp UI

    void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, int instigatorTeam = -1)
    {
        if (isDead) return;
        if (team != -1 && instigatorTeam != -1 && team == instigatorTeam) return; // ff

        currentHealth = Mathf.Max(0, currentHealth - amount);
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = $"HP: {currentHealth}/{maxHealth}";
        }
    }
}