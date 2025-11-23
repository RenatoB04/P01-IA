using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerShield : NetworkBehaviour
{
    public enum ShieldMode { Capacity, Duration }

    [Header("Referências Visuais")]
    [SerializeField] private GameObject shieldVisual;
    [SerializeField] private GameObject pulseVfxPrefab;
    private TextMeshProUGUI shieldTextUI;

    [Header("Configurações")]
    [SerializeField] private ShieldMode shieldMode = ShieldMode.Capacity;
    [SerializeField] private float shieldCapacity = 50f;
    [SerializeField] private float shieldDuration = 5.0f;
    [SerializeField] private float shieldCooldown = 10.0f;

    [SerializeField] private float pulseDamage = 40f;
    [SerializeField] private float pulseRadius = 8.0f;
    [SerializeField] private float pulseCastTime = 0.5f;
    [SerializeField] private float pulseCooldown = 15.0f;

    // Network Variables
    public NetworkVariable<bool> IsShieldActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> ShieldHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> NextShieldReadyTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsPulseCasting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> NextPulseReadyTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        if (shieldVisual != null) shieldVisual.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (shieldVisual != null) shieldVisual.SetActive(IsShieldActive.Value);
        if (IsOwner) StartCoroutine(FindShieldUI());
    }

    private IEnumerator FindShieldUI()
    {
        while (shieldTextUI == null)
        {
            GameObject uiObj = GameObject.FindGameObjectWithTag("ShieldText");
            if (uiObj != null) shieldTextUI = uiObj.GetComponent<TextMeshProUGUI>();
            yield return new WaitForSeconds(1f);
        }
        shieldTextUI.text = "";
    }

    void Update()
    {
        // Sincronia Visual
        if (shieldVisual != null && shieldVisual.activeSelf != IsShieldActive.Value)
            shieldVisual.SetActive(IsShieldActive.Value);

        // --- LÓGICA DO DONO (INPUT) ---
        if (IsOwner)
        {
            UpdateUI();
            HandleInput();
        }
    }

    private void HandleInput()
    {
        // Segurança: Se estiver pausado ou morto, sai
        if (PauseMenuManager.IsPaused) return;
        if (health != null && health.isDead.Value) return;

        // Usa o GameInput para ler as teclas
        if (GameInput.LocalInput != null)
        {
            if (GameInput.LocalInput.ShieldTriggered())
            {
                RequestShieldServerRpc();
            }

            if (GameInput.LocalInput.PulseTriggered())
            {
                RequestPulseServerRpc();
            }
        }
    }

    private void UpdateUI()
    {
        if (shieldTextUI == null) return;
        double now = NetworkManager.Singleton.LocalTime.Time;

        if (IsShieldActive.Value)
        {
            shieldTextUI.text = $"ESCUDO: {ShieldHealth.Value:0}";
            shieldTextUI.color = Color.cyan;
        }
        else if (IsPulseCasting.Value)
        {
            shieldTextUI.text = "A CARREGAR...";
            shieldTextUI.color = Color.yellow;
        }
        else
        {
            string msg = "";
        
            // --- 1. STATUS DO ESCUDO ---
            if (now < NextShieldReadyTime.Value) 
                // Removido o espaço duplo no final
                msg += $"Escudo: {(NextShieldReadyTime.Value - now):0.0}s"; 
            else 
                // Removido o espaço duplo no final
                msg += "Escudo: PRONTO (Z)";

            // --- 2. QUEBRA DE LINHA PARA SEPARAR OS ITENS ---
            msg += "\n"; 

            // --- 3. STATUS DO PULSO ---
            if (now < NextPulseReadyTime.Value) 
                msg += $"Pulso: {(NextPulseReadyTime.Value - now):0.0}s";
            else 
                msg += "Pulso: PRONTO (X)";

            shieldTextUI.text = msg;
            shieldTextUI.color = Color.white;
        }
    }

    // --- RPCs (Mantêm-se iguais) ---
    [ServerRpc]
    public void RequestShieldServerRpc()
    {
        double now = NetworkManager.LocalTime.Time;
        if (now < NextShieldReadyTime.Value || IsShieldActive.Value) return;
        
        IsShieldActive.Value = true;
        NextShieldReadyTime.Value = now + shieldCooldown;
        ShieldHealth.Value = (shieldMode == ShieldMode.Capacity) ? shieldCapacity : 1000f;
        
        if (shieldMode == ShieldMode.Duration) StartCoroutine(ShieldTimer());
    }

    IEnumerator ShieldTimer()
    {
        yield return new WaitForSeconds(shieldDuration);
        if (IsShieldActive.Value) { IsShieldActive.Value = false; ShieldHealth.Value = 0; }
    }

    public float AbsorbDamageServer(float incoming)
    {
        if (!IsServer || !IsShieldActive.Value) return incoming;
        if (shieldMode == ShieldMode.Duration) return 0f;

        float absorbed = Mathf.Min(ShieldHealth.Value, incoming);
        ShieldHealth.Value -= absorbed;
        if (ShieldHealth.Value <= 0) { IsShieldActive.Value = false; ShieldHealth.Value = 0; }
        return incoming - absorbed;
    }

    [ServerRpc]
    public void RequestPulseServerRpc()
    {
        double now = NetworkManager.LocalTime.Time;
        if (now < NextPulseReadyTime.Value || IsPulseCasting.Value) return;
        StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        IsPulseCasting.Value = true;
        yield return new WaitForSeconds(pulseCastTime);
        
        if (health && !health.isDead.Value)
        {
            PlayVfxClientRpc(transform.position);
            Collider[] hits = Physics.OverlapSphere(transform.position, pulseRadius);
            int myTeam = health.team.Value;
            foreach (var c in hits)
            {
                if (c.transform.root == transform.root) continue;
                var h = c.GetComponentInParent<Health>();
                if (h) h.ApplyDamageServer(pulseDamage, myTeam, OwnerClientId, transform.position, true);
            }
        }
        
        IsPulseCasting.Value = false;
        NextPulseReadyTime.Value = NetworkManager.LocalTime.Time + pulseCooldown;
    }

    [ClientRpc]
    void PlayVfxClientRpc(Vector3 p) { if (pulseVfxPrefab) Instantiate(pulseVfxPrefab, p, Quaternion.identity); }
}