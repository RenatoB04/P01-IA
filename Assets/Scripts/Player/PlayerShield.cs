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

    [Header("Tempo máximo do escudo")]
    [Tooltip("Tempo máximo (segundos) que o escudo permanece activo antes de desaparecer automaticamente.")]
    [SerializeField] private float shieldMaxLifetime = 7f; 

    
    public NetworkVariable<bool> IsShieldActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> ShieldHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> NextShieldReadyTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsPulseCasting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> NextPulseReadyTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Health health;

    
    private Coroutine shieldLifetimeCoroutine = null;
    
    private Coroutine shieldDurationCoroutine = null;

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
        
        if (shieldVisual != null && shieldVisual.activeSelf != IsShieldActive.Value)
            shieldVisual.SetActive(IsShieldActive.Value);

        
        if (IsOwner)
        {
            UpdateUI();
            HandleInput();
        }
    }

    private void HandleInput()
    {
        
        if (PauseMenuManager.IsPaused) return;
        if (health != null && health.isDead.Value) return;

        
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
        
            
            if (now < NextShieldReadyTime.Value) 
                msg += $"Escudo: {(NextShieldReadyTime.Value - now):0.0}s"; 
            else 
                msg += "Escudo: PRONTO (Z)";

            
            msg += "\n"; 

            
            if (now < NextPulseReadyTime.Value) 
                msg += $"Pulso: {(NextPulseReadyTime.Value - now):0.0}s";
            else 
                msg += "Pulso: PRONTO (X)";

            shieldTextUI.text = msg;
            shieldTextUI.color = Color.white;
        }
    }

    
    [ServerRpc]
    public void RequestShieldServerRpc()
    {
        double now = NetworkManager.LocalTime.Time;
        if (now < NextShieldReadyTime.Value || IsShieldActive.Value) return;
        
        IsShieldActive.Value = true;
        NextShieldReadyTime.Value = now + shieldCooldown;
        ShieldHealth.Value = (shieldMode == ShieldMode.Capacity) ? shieldCapacity : 1000f;

        
        if (shieldLifetimeCoroutine != null) { StopCoroutine(shieldLifetimeCoroutine); shieldLifetimeCoroutine = null; }
        if (shieldDurationCoroutine != null) { StopCoroutine(shieldDurationCoroutine); shieldDurationCoroutine = null; }

        
        shieldLifetimeCoroutine = StartCoroutine(ShieldMaxLifetimeCoroutine());

        
        if (shieldMode == ShieldMode.Duration)
        {
            shieldDurationCoroutine = StartCoroutine(ShieldTimer());
        }
    }

    IEnumerator ShieldTimer()
    {
        yield return new WaitForSeconds(shieldDuration);
        
        DeactivateShieldServer();
        shieldDurationCoroutine = null;
    }

    IEnumerator ShieldMaxLifetimeCoroutine()
    {
        yield return new WaitForSeconds(shieldMaxLifetime);
        DeactivateShieldServer();
        shieldLifetimeCoroutine = null;
    }

    
    private void DeactivateShieldServer()
    {
        if (!IsServer) return;
        if (!IsShieldActive.Value) return;

        IsShieldActive.Value = false;
        ShieldHealth.Value = 0f;

        
        if (shieldLifetimeCoroutine != null) { StopCoroutine(shieldLifetimeCoroutine); shieldLifetimeCoroutine = null; }
        if (shieldDurationCoroutine != null) { StopCoroutine(shieldDurationCoroutine); shieldDurationCoroutine = null; }
    }

    public float AbsorbDamageServer(float incoming)
    {
        if (!IsServer || !IsShieldActive.Value) return incoming;
        if (shieldMode == ShieldMode.Duration) return 0f;

        float absorbed = Mathf.Min(ShieldHealth.Value, incoming);
        ShieldHealth.Value -= absorbed;
        if (ShieldHealth.Value <= 0f)
        {
            
            DeactivateShieldServer();
        }
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
