using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode; 

public class BOTDeath : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Componente que tem a bool isDead (ex: Health script).")]
    public MonoBehaviour health;  // Referência ao script que contém o estado de vida
    [Tooltip("Nome exato da bool no script de vida (case-sensitive).")]
    public string isDeadField = "isDead";  // Nome do campo ou propriedade que indica se o bot está morto

    [Header("Comportamento")]
    [Tooltip("Atraso antes de desaparecer (segundos).")]
    public float delay = 0f; // Tempo até o bot desaparecer/apagar

    [Tooltip("Se true: Destroy(gameObject); se false: SetActive(false).")]
    public bool destroyInstead = true; // Define se o bot é destruído ou apenas desativado

    [Tooltip("Desativar collider ao morrer.")]
    public bool disableColliderOnDeath = true; // Desativa colliders

    [Tooltip("Desativar Animator ao morrer.")]
    public bool disableAnimatorOnDeath = true; // Desativa Animator

    [Tooltip("Desativar NavMeshAgent ao morrer.")]
    public bool disableNavMeshAgentOnDeath = true; // Desativa NavMeshAgent

    // Evento individual chamado quando este bot morre
    public event Action<BOTDeath> OnDied;

    // Evento global chamado quando qualquer bot morre
    public static event Action OnAnyBotKilled;

    bool hasDied = false; // Flag para evitar múltiplas chamadas de morte

    void Update()
    {
        if (hasDied) return; // Se já morreu, não faz nada

        if (IsHealthDead()) // Verifica estado de vida
        {
            HandleDeath(); // Executa a lógica de morte
        }
    }

    // Verifica se o bot está morto através do campo ou propriedade especificada
    bool IsHealthDead()
    {
        if (!health || string.IsNullOrEmpty(isDeadField))
            return false;

        var type = health.GetType();

        // Procura um campo com o nome especificado
        var field = type.GetField(isDeadField);
        if (field != null)
        {
            // Se for bool simples
            if (field.FieldType == typeof(bool))
            {
                return (bool)field.GetValue(health);
            }

            // Se for NetworkVariable<bool>
            if (field.FieldType == typeof(NetworkVariable<bool>))
            {
                var netVar = (NetworkVariable<bool>)field.GetValue(health);
                if (netVar != null)
                    return netVar.Value;
            }
        }

        // Procura uma propriedade do tipo bool
        var prop = type.GetProperty(isDeadField);
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            return (bool)prop.GetValue(health);
        }

        Debug.LogWarning($"[BOTDeath] Não foi possível encontrar o campo/propriedade '{isDeadField}' do tipo 'bool' ou 'NetworkVariable<bool>' no script '{health.GetType().Name}'.");
        return false;
    }

    // Executa toda a lógica de morte
    public void HandleDeath()
    {
        if (hasDied) return; 
        hasDied = true;

        // Desativa colliders se necessário
        if (disableColliderOnDeath)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        // Desativa Animator se necessário
        if (disableAnimatorOnDeath)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim) anim.enabled = false;
        }

        // Desativa NavMeshAgent se necessário
        if (disableNavMeshAgentOnDeath)
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent) agent.enabled = false;
        }

        // Dispara eventos de morte
        try { OnDied?.Invoke(this); } catch { }
        try { OnAnyBotKilled?.Invoke(); } catch { }

        // Começa coroutine para desaparecer ou destruir o bot
        StartCoroutine(Disappear());
    }

    // Coroutine que remove o bot do jogo após o delay
    IEnumerator Disappear()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (destroyInstead)
            Destroy(gameObject);      // Destrói o objeto
        else
            gameObject.SetActive(false); // Apenas desativa o objeto
    }
}
