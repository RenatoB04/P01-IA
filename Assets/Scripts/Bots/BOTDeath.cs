using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode; // <-- LINHA ADICIONADA

public class BOTDeath : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Componente que tem a bool isDead (ex: Health script).")]
    public MonoBehaviour health;
    [Tooltip("Nome exato da bool no script de vida (case-sensitive).")]
    public string isDeadField = "isDead";

    [Header("Comportamento")]
    [Tooltip("Atraso antes de desaparecer (segundos).")]
    public float delay = 0f;

    [Tooltip("Se true: Destroy(gameObject); se false: SetActive(false).")]
    public bool destroyInstead = true;

    [Tooltip("Desativar collider ao morrer.")]
    public bool disableColliderOnDeath = true;

    [Tooltip("Desativar Animator ao morrer.")]
    public bool disableAnimatorOnDeath = true;

    [Tooltip("Desativar NavMeshAgent ao morrer.")]
    public bool disableNavMeshAgentOnDeath = true;

    // Evento por bot (para scripts no mesmo GameObject)
    public event Action<BOTDeath> OnDied;

    // Evento global (usado pelo BotSpawner_Proto)
    public static event Action OnAnyBotKilled;

    bool hasDied = false;

    void Update()
    {
        if (hasDied) return;

        if (IsHealthDead())
        {
            HandleDeath();
        }
    }

    // ---------- FUNÇÃO MODIFICADA ----------
    bool IsHealthDead()
    {
        if (!health || string.IsNullOrEmpty(isDeadField))
            return false;

        var type = health.GetType();

        // Tenta campo (para NetworkVariable<bool> e bool normal)
        var field = type.GetField(isDeadField);
        if (field != null)
        {
            // Se for um bool normal
            if (field.FieldType == typeof(bool))
            {
                return (bool)field.GetValue(health);
            }

            // --- CORREÇÃO ADICIONADA ---
            // Se for uma NetworkVariable<bool>
            if (field.FieldType == typeof(NetworkVariable<bool>))
            {
                // Pede o valor de dentro da NetworkVariable
                var netVar = (NetworkVariable<bool>)field.GetValue(health);
                if (netVar != null)
                    return netVar.Value;
            }
            // --- FIM DA CORREÇÃO ---
        }

        // Tenta propriedade (para bool normal)
        var prop = type.GetProperty(isDeadField);
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            return (bool)prop.GetValue(health);
        }

        Debug.LogWarning($"[BOTDeath] Não foi possível encontrar o campo/propriedade '{isDeadField}' do tipo 'bool' ou 'NetworkVariable<bool>' no script '{health.GetType().Name}'.");
        return false;
    }
    // ---------- FIM DA MODIFICAÇÃO ----------

    public void HandleDeath()
    {
        if (hasDied) return; // Evita chamadas múltiplas
        hasDied = true;

        // desativar componentes que não interessam depois de morto
        if (disableColliderOnDeath)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        if (disableAnimatorOnDeath)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim) anim.enabled = false;
        }

        if (disableNavMeshAgentOnDeath)
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent) agent.enabled = false;
        }

        // notificar listeners
        try { OnDied?.Invoke(this); } catch { }
        try { OnAnyBotKilled?.Invoke(); } catch { }

        // iniciar desaparecimento
        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (destroyInstead)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}