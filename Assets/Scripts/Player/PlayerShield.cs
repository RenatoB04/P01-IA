using Unity.Netcode;
using UnityEngine;
using System.Collections; // Para Coroutines

public class PlayerShield : NetworkBehaviour
{
    // ======================================================
    // DEFINIÇÃO DO ENUM (DENTRO DA CLASSE)
    // ======================================================
    public enum ShieldMode 
    { 
        Capacity, // Modo 1: 100 HP
        Duration  // Modo 2: 2 Segundos
    }
    // ======================================================

    // --- Referência interna ---
    private Health health; // Precisamos disto para saber a equipa/ID

    [Header("Modo Escudo")]
    [Tooltip("Capacity = Absorve 100 HP. Duration = Fica 2s ativo.")]
    [SerializeField] private ShieldMode shieldMode = ShieldMode.Capacity;

    [Header("Configurações Escudo")]
    [SerializeField] private float shieldCapacity = 100f; // 100 PV
    [SerializeField] private float shieldDuration = 2.0f;  // 2 Segundos
    [SerializeField] private float shieldCooldown = 30.0f; // 30 Segundos

    // ======================================================
    // --- Configurações do Pulso de Energia ---
    // ======================================================
    [Header("Configurações Energy Pulse")]
    [SerializeField] private float pulseDamage = 40f;       // 40 PV
    [SerializeField] private float pulseRadius = 4.0f;      // 4 metros
    [SerializeField] private float pulseCastTime = 1.5f;    // 1.5 segundos
    [SerializeField] private float pulseCooldown = 45.0f;   // 45 segundos

    // ======================================================
    // ESTADO (NetworkVariables)
    // ======================================================

    // --- Estado do Escudo ---
    public NetworkVariable<bool> IsShieldActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<double> NextShieldReadyTime = new NetworkVariable<double>(
        0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<float> ShieldHealth = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ======================================================
    // --- Estado do Pulso de Energia ---
    // ======================================================
    
    public NetworkVariable<double> NextPulseReadyTime = new NetworkVariable<double>(
        0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<bool> IsPulseCasting = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private Coroutine pulseCastCoroutine;

    // --- Awake ---
    void Awake()
    {
        health = GetComponent<Health>();
    }

    // ======================================================
    // LÓGICA DO SERVIDOR (ESCUDO)
    // ======================================================

    [ServerRpc]
    public void RequestShieldServerRpc()
    {
        double now = NetworkManager.LocalTime.Time;

        if (now < NextShieldReadyTime.Value || IsShieldActive.Value)
        {
            Debug.Log($"[Servidor] Pedido de escudo rejeitado.");
            return;
        }
        if (health != null && health.isDead.Value)
        {
            return;
        }

        NextShieldReadyTime.Value = now + shieldCooldown;
        IsShieldActive.Value = true;

        if (shieldMode == ShieldMode.Capacity)
        {
            ShieldHealth.Value = shieldCapacity;
            Debug.Log($"[Servidor] Escudo (Capacity) ATIVADO com {ShieldHealth.Value} HP.");
        }
        else // shieldMode == ShieldMode.Duration
        {
            ShieldHealth.Value = 0; 
            StartCoroutine(ShieldActiveTimerServer());
            Debug.Log($"[Servidor] Escudo (Duration) ATIVADO por {shieldDuration}s.");
        }
    }

    private IEnumerator ShieldActiveTimerServer()
    {
        yield return new WaitForSeconds(shieldDuration);
        if (IsShieldActive.Value)
        {
            DeactivateShieldServer();
            Debug.Log($"[Servidor] Escudo (Duration) DESATIVADO (tempo esgotou).");
        }
    }

    private void DeactivateShieldServer()
    {
        IsShieldActive.Value = false;
        ShieldHealth.Value = 0;
    }

    public float AbsorbDamageServer(float incomingDamage)
    {
        if (!IsServer || !IsShieldActive.Value)
        {
            return incomingDamage; 
        }
        if (shieldMode == ShieldMode.Duration)
        {
            Debug.Log("[Servidor] Escudo (Duration) absorveu TODO o dano.");
            return 0f; 
        }
        if (shieldMode == ShieldMode.Capacity)
        {
            float absorbed = Mathf.Min(ShieldHealth.Value, incomingDamage);
            float remainingDamage = incomingDamage - absorbed; 
            ShieldHealth.Value -= absorbed; 
            Debug.Log($"[Servidor] Escudo (Capacity) absorveu {absorbed}. Resta {ShieldHealth.Value} HP.");
            if (ShieldHealth.Value <= 0)
            {
                DeactivateShieldServer();
                Debug.Log("[Servidor] Escudo (Capacity) QUEBROU.");
            }
            return remainingDamage; 
        }
        return incomingDamage;
    }
    
    // ======================================================
    // LÓGICA DO SERVIDOR (PULSO DE ENERGIA)
    // ======================================================

    [ServerRpc]
    public void RequestPulseServerRpc()
    {
        if (health == null) return; 

        double now = NetworkManager.LocalTime.Time;

        if (health.isDead.Value) return;
        if (now < NextPulseReadyTime.Value)
        {
            Debug.Log("[Servidor] Pedido de Pulso rejeitado (Cooldown).");
            return;
        }
        if (IsPulseCasting.Value)
        {
            Debug.Log("[Servidor] Pedido de Pulso rejeitado (Já está a carregar).");
            return;
        }
        
        pulseCastCoroutine = StartCoroutine(PulseCastAndExecuteServer());
    }

    private IEnumerator PulseCastAndExecuteServer()
    {
        IsPulseCasting.Value = true;
        Debug.Log($"[Servidor] Jogador {OwnerClientId} a carregar o Pulso...");

        yield return new WaitForSeconds(pulseCastTime);

        if (health == null || health.isDead.Value)
        {
            Debug.Log($"[Servidor] Pulso cancelado (jogador morreu durante o cast).");
            IsPulseCasting.Value = false;
            yield break;
        }

        Debug.Log($"[Servidor] Jogador {OwnerClientId} EXECUTOU o Pulso!");
        ExecutePulseServer(); // <--- VAMOS VERIFICAR ESTA FUNÇÃO

        IsPulseCasting.Value = false;
        NextPulseReadyTime.Value = NetworkManager.LocalTime.Time + pulseCooldown;
    }

    // ======================================================
    // --- FUNÇÃO MODIFICADA COM MAIS LOGS DE DEBUG ---
    // ======================================================
    private void ExecutePulseServer()
    {
        Vector3 center = transform.position;
        int instigatorTeam = health.team.Value;
        ulong instigatorClientId = OwnerClientId;

        // --- DEBUG 1 ---
        // Vamos ver se o OverlapSphere encontra ALGUMA COISA
        Collider[] hits = Physics.OverlapSphere(center, pulseRadius);
        Debug.Log($"[Servidor] OverlapSphere encontrou {hits.Length} colliders num raio de {pulseRadius}m.");

        foreach (var col in hits)
        {
            if (col == null) continue;

            Health targetHealth = col.GetComponentInParent<Health>();
            
            // --- DEBUG 2 ---
            // Vamos ver o que ele encontrou e por que filtrou
            if (targetHealth == null)
            {
                // Encontrou um objeto (como o chão, uma parede, ou um bot sem o script Health)
                Debug.Log($"[Servidor] Pulso atingiu '{col.name}', mas não tem script 'Health'. Ignorando.");
                continue;
            }

            if (targetHealth.transform.root == this.transform.root)
            {
                // Encontrou o próprio jogador
                Debug.Log($"[Servidor] Pulso atingiu-se a si próprio ('{col.name}'). Ignorando.");
                continue;
            }
            
            // Se chegou aqui, é um alvo válido!
            Debug.Log($"[Servidor] Pulso ATINGIU ALVO VÁLIDO: {targetHealth.name}. A aplicar {pulseDamage} de dano.");
            targetHealth.ApplyDamageServer(pulseDamage, instigatorTeam, instigatorClientId, center, true);
        }
    }
}