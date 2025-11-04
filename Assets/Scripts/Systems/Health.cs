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

    // ----- MODIFICAÇÃO 1 (Referência ao Escudo) -----
    private PlayerShield playerShield;

    // Scoring
    private ulong lastInstigatorClientId = ulong.MaxValue;
    private Coroutine uiFinderCo;

    void Awake()
    {
        // ----- MODIFICAÇÃO 2 (Obter o Escudo) -----
        playerShield = GetComponent<PlayerShield>();
        
        UpdateHealthUI(maxHealth);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
            if (team.Value == -1)
                team.Value = (int)OwnerClientId;
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
        else
        {
            Debug.LogWarning($"{name}/Health: Não encontrei UI 'HealthText'.");
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
        Debug.Log($"[Health] {name} HP: {prev:0} -> {curr:0}");
        UpdateHealthUI(curr);
        OnHealthChanged?.Invoke(curr, maxHealth);
    }

    private void OnIsDeadChanged(bool prev, bool curr)
    {
        Debug.Log($"[Health] {name} isDead: {prev} -> {curr}");
        if (curr && !prev)
            OnDied?.Invoke();
    }

    // -------- API pública (compatível) --------

    // Cliente pode chamar (ex.: explosão local) → vai ao servidor via RPC
    public void TakeDamage(float amount, int instigatorTeam = -1, ulong instigatorClientId = ulong.MaxValue)
        => TakeDamageServerRpc(amount, instigatorTeam, instigatorClientId, Vector3.zero, false);

    // Usado por projéteis: inclui posição e sinaliza para UI de direção
    public void TakeDamageFrom(float amount, int instigatorTeam, Transform attacker, Vector3 hitWorldPos, ulong instigatorClientId = ulong.MaxValue)
        => TakeDamageServerRpc(amount, instigatorTeam, instigatorClientId, attacker ? attacker.position : hitWorldPos, true);

    // -------- Núcleo server-authoritative --------

    // Chamada directa pelo SERVIDOR (p.ex. pela bala)
    public void ApplyDamageServer(float amount, int instigatorTeam, ulong instigatorClientId, Vector3 hitWorldPos, bool showIndicator = true)
    {
        if (!IsServer) return;
        if (isDead.Value) return;

        amount = Mathf.Clamp(amount, 0f, maxHealth * 2f);
        if (amount <= 0f) return;

        // ==========================================================
        // ===== MODIFICAÇÃO 3 (Bloco de Lógica do Escudo)      =====
        // ==========================================================
        
        // Verifica se temos um escudo e se ele está ATIVO
        if (playerShield != null && playerShield.IsShieldActive.Value)
        {
            // Pede ao escudo para absorver o dano.
            // O 'amount' será atualizado para o dano que "passou".
            amount = playerShield.AbsorbDamageServer(amount);

            // Se o escudo absorveu TUDO (ou quase tudo),
            // não há mais nada a fazer nesta função.
            if (amount <= 0.01f)
            {
                return; 
            }
        }
        // ==========================================================
        // ===== FIM DO BLOCO DE CÓDIGO                         =====
        // ==========================================================


        // Friendly fire
        if (team.Value != -1 && instigatorTeam != -1 && team.Value == instigatorTeam)
        {
            Debug.Log($"[Health] FF ignorado em {name}. team={team.Value}, instigatorTeam={instigatorTeam}");
            return;
        }

        lastInstigatorClientId = instigatorClientId;

        float old = currentHealth.Value;
        float next = Mathf.Max(0f, old - amount);
        if (Mathf.Approximately(old, next)) return;

        currentHealth.Value = next;
        Debug.Log($"[Health] {name} levou {amount} de dano. Agora: {next:0}/{maxHealth:0}");

        if (next < old)
            OnTookDamage?.Invoke(amount, null);

        // Feedback no alvo (só para o dono, via ClientRpc dirigido)
        if (showIndicator)
        {
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            DamageIndicatorClientRpc(hitWorldPos, target);
        }

        if (next <= 0f && !isDead.Value)
        {
            isDead.Value = true;
            Debug.Log($"[Health] {name} morreu. isDead=true");
            TryAwardKillToLastInstigator();
        }
    }

    // Entrada RPC para clientes chamarem dano
    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float amount, int instigatorTeam, ulong instigatorClientId, Vector3 hitWorldPos, bool showIndicator)
    {
        ApplyDamageServer(amount, instigatorTeam, instigatorClientId, hitWorldPos, showIndicator);
    }

    [ClientRpc]
    private void DamageIndicatorClientRpc(Vector3 sourceWorldPos, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        if (DamageIndicatorUI.Instance)
            DamageIndicatorUI.Instance.RegisterHit(sourceWorldPos, 0f);
    }

    private void TryAwardKillToLastInstigator()
    {
        if (!IsServer) return;
        if (lastInstigatorClientId == ulong.MaxValue) return;
        if (lastInstigatorClientId == OwnerClientId) return; // suicídio: sem pontos

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(lastInstigatorClientId, out var client) &&
            client != null && client.PlayerObject != null)
        {
            var ps = client.PlayerObject.GetComponent<PlayerScore>();
            if (ps != null) ps.AwardKillAndPoints();
        }

        lastInstigatorClientId = ulong.MaxValue;
    }

    // -------- Cura/Reset --------
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

    public void ResetFullHealth() => ResetHealthServerRpc();

    [ServerRpc(RequireOwnership = false)]
    private void ResetHealthServerRpc()
    {
        isDead.Value = false;
        currentHealth.Value = maxHealth;
        Debug.Log($"[Health] {name} reset para {maxHealth} HP e isDead=false");
    }

    // -------- UI --------
    public void UpdateHealthUI(float v)
    {
        if (healthText != null)
            healthText.text = $"HP: {v:0}/{maxHealth:0}";
    }
}