using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System;
using Unity.Netcode;
using System.Collections;

public class Health : NetworkBehaviour
{
    [Header("Config")]
    public float maxHealth = 100f;

    
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> team = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Events")]
    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent OnDied;
    public event Action<float, Transform> OnTookDamage;

    [Header("UI (Opcional)")]
    [HideInInspector] public TextMeshProUGUI healthText;

    private PlayerShield playerShield;
    private ulong lastInstigatorClientId = ulong.MaxValue;
    private Coroutine uiFinderCo;

    
    private float timeOfDeath = 0f;

    void Awake()
    {
        playerShield = GetComponent<PlayerShield>();
        UpdateHealthUI(maxHealth);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (team.Value == -1)
            {
                
                if (GetComponent<BotAI_Proto>() != null)
                    team.Value = -2;
                else
                    team.Value = (int)OwnerClientId;
            }
        }

        currentHealth.OnValueChanged += OnHealthValueChanged;
        isDead.OnValueChanged += OnIsDeadChanged;

        UpdateHealthUI(currentHealth.Value);
        OnHealthChanged?.Invoke(currentHealth.Value, maxHealth);

        if (IsOwner)
            uiFinderCo = StartCoroutine(FindUIRefresh());
    }

    private IEnumerator FindUIRefresh()
    {
        const int safetyFrames = 600;
        int frames = 0;
        GameObject healthTextObj = null;

        while (healthTextObj == null && frames < safetyFrames)
        {
            yield return null;
            frames++;
            healthTextObj = GameObject.FindWithTag("HealthText");
            if (healthTextObj == null)
            {
                var byName = GameObject.Find("HealthText");
                if (byName && byName.GetComponent<TextMeshProUGUI>() != null)
                    healthTextObj = byName;
            }
        }

        if (healthTextObj != null)
        {
            healthText = healthTextObj.GetComponent<TextMeshProUGUI>();
            UpdateHealthUI(currentHealth.Value);
        }
        uiFinderCo = null;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthValueChanged;
        isDead.OnValueChanged -= OnIsDeadChanged;
        if (uiFinderCo != null) { StopCoroutine(uiFinderCo); uiFinderCo = null; }
    }

    private void OnHealthValueChanged(float prev, float curr)
    {
        UpdateHealthUI(curr);
        OnHealthChanged?.Invoke(curr, maxHealth);
    }

    private void OnIsDeadChanged(bool prev, bool curr)
    {
        if (curr && !prev)
            OnDied?.Invoke();
    }

    
    
    

    public void ApplyDamageServer(float amount, int instigatorTeam, ulong instigatorClientId, Vector3 hitWorldPos, bool showIndicator = true)
    {
        if (!IsServer) return;
        if (isDead.Value) return; 

        amount = Mathf.Clamp(amount, 0f, maxHealth * 2f);
        if (amount <= 0f) return;

        
        if (playerShield != null && playerShield.IsShieldActive.Value)
        {
            amount = playerShield.AbsorbDamageServer(amount);
            if (amount <= 0.01f) return;
        }

        
        if (team.Value != -1 && instigatorTeam != -1 && team.Value == instigatorTeam)
            return;

        lastInstigatorClientId = instigatorClientId;
        float oldHealth = currentHealth.Value;
        float newHealth = Mathf.Max(0f, oldHealth - amount);

        if (Mathf.Approximately(oldHealth, newHealth)) return;

        currentHealth.Value = newHealth;

        if (newHealth < oldHealth)
            OnTookDamage?.Invoke(amount, null);

        
        if (showIndicator)
        {
            var clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            DamageIndicatorClientRpc(hitWorldPos, amount, clientParams);
        }

        
        if (newHealth <= 0.01f && !isDead.Value)
        {
            isDead.Value = true;
            
            timeOfDeath = Time.time;

            Debug.Log($"[Health] {name} (Team {team.Value}) morreu.");
            TryAwardKillToLastInstigator();
        }
    }

    [ClientRpc]
    private void DamageIndicatorClientRpc(Vector3 sourceWorldPos, float damageAmount, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        if (DamageIndicatorUI.Instance != null)
            DamageIndicatorUI.Instance.RegisterHit(sourceWorldPos, damageAmount);
    }

    private void TryAwardKillToLastInstigator()
    {
        if (!IsServer) return;
        if (lastInstigatorClientId == ulong.MaxValue) return;
        if (lastInstigatorClientId == OwnerClientId) return;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(lastInstigatorClientId, out var client) &&
            client != null && client.PlayerObject != null)
        {
            var ps = client.PlayerObject.GetComponent<PlayerScore>();
            if (ps != null) ps.AwardKillAndPoints();
        }
        lastInstigatorClientId = ulong.MaxValue;
    }

    
    
    

    public void ResetFullHealth() => ResetHealthServerRpc();

    [ServerRpc(RequireOwnership = false)]
    private void ResetHealthServerRpc()
    {
        
        
        
        if (isDead.Value && Time.time < timeOfDeath + 2.0f)
        {
            return;
        }

        isDead.Value = false;
        currentHealth.Value = maxHealth;
    }

    public void Heal(float amount)
    {
        if (isDead.Value) return;
        HealServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealServerRpc(float amount)
    {
        if (isDead.Value) return;
        amount = Mathf.Clamp(amount, 0f, maxHealth * 2f);
        if (amount <= 0f) return;
        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + amount);
    }

    public void TakeDamage(float amount) => TakeDamageServerRpc(amount, -1, ulong.MaxValue, Vector3.zero, false);

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float amount, int instigatorTeam, ulong instigatorClientId, Vector3 hitWorldPos, bool showIndicator)
    {
        ApplyDamageServer(amount, instigatorTeam, instigatorClientId, hitWorldPos, showIndicator);
    }

    private void UpdateHealthUI(float v)
    {
        if (healthText != null) healthText.text = $"{v:0}";
    }
}