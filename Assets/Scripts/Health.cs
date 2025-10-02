using UnityEngine;
using UnityEngine.Events;

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

    void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, int instigatorTeam = -1)
    {
        if (isDead) return;
        if (team != -1 && instigatorTeam != -1 && team == instigatorTeam) return; // ff

        currentHealth = Mathf.Max(0, currentHealth - amount);
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
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
