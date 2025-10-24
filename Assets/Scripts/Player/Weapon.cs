using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static UnityEngine.Time; 

public class Weapon : MonoBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform cam;
    [SerializeField] ParticleSystem muzzleFlash;   // VFX
    [SerializeField] AudioSource fireAudio;        // SFX

    [Header("Input")]
    [SerializeField] InputActionReference shootAction;
    [SerializeField] InputActionReference reloadAction;   // ⬅ reload

    [Header("Settings (fallbacks se não houver config)")]
    [SerializeField] float bulletSpeed = 40f;
    [SerializeField] float fireRate = 0.12f;
    [SerializeField] float maxAimDistance = 200f;

    [Header("Behaviour")]
    [Tooltip("Player: TRUE (só dispara com WeaponConfig). Bot: FALSE (usa campos locais).")]
    [SerializeField] bool requireConfigForFire = true;   // Player=TRUE, Bots=FALSE

    [Header("HUD")]
    [SerializeField] AmmoUI ammoUI;  // ⬅ liga no Canvas

    // Auto-config
    WeaponConfig[] allConfigs;
    WeaponConfig activeConfig;
    Component weaponSwitcher;

    // Estado de tiro
    float nextFireTimeUnscaled; 
    CharacterController playerCC;

    // ---- AMMO/RELOAD (apenas para Player) ----
    class AmmoState { public int inMag; public int reserve; }
    readonly Dictionary<WeaponConfig, AmmoState> ammoByConfig = new();
    int currentAmmo, reserveAmmo;
    bool isReloading;

    void Awake()
    {
        // CORREÇÃO DA CÂMARA (uso da referência estável)
        if (!cam)
        {
            if (FP_Controller_IS.PlayerCameraRoot != null)
            {
                cam = FP_Controller_IS.PlayerCameraRoot;
            }
            else if (Camera.main)
            {
                cam = Camera.main.transform;
            }
        }

        playerCC = GetComponentInParent<CharacterController>();

        if (GetComponentInParent<BotCombat>() != null)
            requireConfigForFire = false;

        allConfigs = GetComponentsInChildren<WeaponConfig>(true);
        weaponSwitcher = GetComponent<WeaponSwitcher>();

        RefreshActiveConfig(applyImmediately: true);
        UpdateHUD();
    }

    void OnEnable()
    {
        // Garante que o Input está ligado quando a arma está ativa
        if (shootAction) shootAction.action.Enable();
        if (reloadAction) reloadAction.action.Enable();
        
        // Reset de emergência do estado da arma (limpa cooldown e isReloading)
        ResetWeaponState();
    }

    void OnDisable()
    {
        // guarda munição atual no dicionário quando a arma sair de ativa
        if (requireConfigForFire && activeConfig && ammoByConfig.ContainsKey(activeConfig))
        {
            ammoByConfig[activeConfig].inMag = currentAmmo;
            ammoByConfig[activeConfig].reserve = reserveAmmo;
        }

        // desativa inputs
        if (shootAction) shootAction.action.Disable();
        if (reloadAction) reloadAction.action.Disable();
        
        isReloading = false;
        StopAllCoroutines(); 
    }
    
    // MÉTODO DE EMERGÊNCIA (Reset de estado)
    public void ResetWeaponState()
    {
        nextFireTimeUnscaled = Time.unscaledTime; 
        isReloading = false;
        StopAllCoroutines();
    }

    void Update()
    {
        RefreshActiveConfig(applyImmediately: true);

        if (requireConfigForFire && activeConfig == null) return;
        
        // CORREÇÃO CRÍTICA: Se o input for NULL (perdido), tentamos forçar o enable
        if (shootAction != null && !shootAction.action.enabled)
        {
            shootAction.action.Enable();
        }

        // 1. INPUT DE RECARGA MANUAL
        if (requireConfigForFire && reloadAction && reloadAction.action.WasPressedThisFrame())
        {
            TryReload();
        }

        // 2. CORREÇÃO DE BLOQUEIO E AUTO-RELOAD FORÇADO: 
        if (requireConfigForFire && currentAmmo <= 0 && reserveAmmo > 0 && !isReloading)
        {
            TryReload();
        }
        
        // Bloqueia o tiro durante a recarga
        if (isReloading) return;

        bool automatic = activeConfig ? activeConfig.automatic : false;
        float useFireRate = activeConfig ? activeConfig.fireRate : fireRate;

        // CRÍTICO: Não use shootAction.action.IsPressed() se for nulo!
        bool wantsShoot = shootAction != null && (automatic
            ? shootAction.action.IsPressed()
            : shootAction.action.WasPressedThisFrame());
        
        // Verifica o cooldown usando tempo NÃO ESCALADO
        if (!wantsShoot || Time.unscaledTime < nextFireTimeUnscaled)
        {
            return;
        }

        // 3. Lógica de Disparo
        if (requireConfigForFire)
        {
            if (currentAmmo <= 0)
            {
                // Toca som seco (chega aqui se TryReload() falhou ou reserva está a 0)
                if (fireAudio && activeConfig && activeConfig.emptyClickSfx)
                    fireAudio.PlayOneShot(activeConfig.emptyClickSfx);
                return; 
            }
            currentAmmo--;
        }

        Shoot();
        
        // Define o novo cooldown usando tempo NÃO ESCALADO
        nextFireTimeUnscaled = Time.unscaledTime + useFireRate;

        if (requireConfigForFire)
        {
            UpdateHUD();
            // Verifica Auto-Reload *após* o tiro ter esvaziado o carregador
            if (currentAmmo == 0 && reserveAmmo > 0) TryReload();
        }
    }

    // Chamado pelos bots
    public void ShootExternally()
    {
        if (requireConfigForFire && activeConfig == null) return;

        float useFireRate = activeConfig ? activeConfig.fireRate : fireRate;
        
        // CRÍTICO: Bots também usam tempo não escalado
        if (Time.unscaledTime >= nextFireTimeUnscaled)
        {
            Shoot();
            nextFireTimeUnscaled = Time.unscaledTime + useFireRate;
        }
    }

    void Shoot()
    {
        if (requireConfigForFire && activeConfig == null) return;

        Transform useFP = activeConfig ? activeConfig.firePoint : firePoint;
        GameObject useBullet = activeConfig ? activeConfig.bulletPrefab : bulletPrefab;
        ParticleSystem useMuzzle = activeConfig ? activeConfig.muzzleFlashPrefab : muzzleFlash;
        float useSpeed = activeConfig ? activeConfig.bulletSpeed : bulletSpeed;
        float useMaxDist = activeConfig ? activeConfig.maxAimDistance : maxAimDistance;

        if (!useBullet || !useFP) return;

        Vector3 dir;
        Ray ray = new Ray(cam ? cam.position : useFP.position, cam ? cam.forward : useFP.forward);
        if (Physics.Raycast(ray, out var hit, useMaxDist, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - useFP.position).normalized;
        else
            dir = (ray.GetPoint(useMaxDist) - useFP.position).normalized;

        var bullet = Instantiate(useBullet, useFP.position, Quaternion.LookRotation(dir));
        bullet.transform.position += dir * 0.2f;

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = dir * useSpeed; 
        }

        if (bullet.TryGetComponent<BulletProjectile>(out var bp))
        {
            var h = GetComponentInParent<Health>();
            if (h) bp.ownerTeam = h.team;
            bp.ownerRoot = h ? h.transform.root : transform.root;
        }

        if (useMuzzle)
        {
            var fx = Instantiate(useMuzzle, useFP.position, useFP.rotation, useFP);
            fx.Play();
            Destroy(fx.gameObject, 0.2f);
        }

        var fireClip = activeConfig ? activeConfig.fireSfx : null;
        if (fireAudio && fireClip) fireAudio.PlayOneShot(fireAudio.clip);
        else if (fireAudio && fireAudio.clip) fireAudio.PlayOneShot(fireAudio.clip);

        CrosshairUI.Instance?.Kick();
    }
    
    // NOVO: Adiciona munição de reserva (para Pickups)
    public void AddReserveAmmo(int amount)
    {
        if (!requireConfigForFire || activeConfig == null) return;
        if (amount <= 0) return;

        reserveAmmo = Mathf.Max(0, reserveAmmo + amount);
        UpdateHUD();

        // Tenta fazer auto-reload se o carregador estiver vazio
        if (currentAmmo == 0) TryReload();
    }


    // ---------- AMMO / RELOAD ----------
    void TryReload()
    {
        if (!requireConfigForFire || activeConfig == null) return;
        if (isReloading) return;
        if (currentAmmo >= activeConfig.magSize) return;
        if (reserveAmmo <= 0) return;

        StopAllCoroutines(); 
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        
        if (fireAudio && activeConfig && activeConfig.reloadSfx)
            fireAudio.PlayOneShot(activeConfig.reloadSfx);

        yield return new WaitForSecondsRealtime(activeConfig.reloadTime); 

        int needed = activeConfig.magSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        isReloading = false;
        UpdateHUD();
    }

    void UpdateHUD()
    {
        if (!requireConfigForFire) return; 
        ammoUI?.Set(currentAmmo, reserveAmmo);
    }

    // ---------- helpers ----------
    public void SetActiveWeapon(GameObject weaponGO)
    {
        activeConfig = weaponGO ? weaponGO.GetComponent<WeaponConfig>() : null;
        RefreshActiveConfig(applyImmediately: true);
    }

    void RefreshActiveConfig(bool applyImmediately)
    {
        var newCfg = FindActiveConfig();
        if (newCfg == activeConfig) return;

        activeConfig = newCfg;
        isReloading = false; 

        if (applyImmediately && activeConfig != null)
        {
            // aplicar valores de tiro
            firePoint = activeConfig.firePoint ?? firePoint;
            bulletPrefab = activeConfig.bulletPrefab ?? bulletPrefab;
            muzzleFlash = activeConfig.muzzleFlashPrefab ?? muzzleFlash;
            bulletSpeed = activeConfig.bulletSpeed;
            fireRate = activeConfig.fireRate;
            maxAimDistance = activeConfig.maxAimDistance;

            // inicializar/recuperar munição deste arma
            if (!ammoByConfig.TryGetValue(activeConfig, out var st))
            {
                st = new AmmoState
                {
                    inMag = Mathf.Max(0, activeConfig.magSize),
                    reserve = Mathf.Max(0, activeConfig.startingReserve)
                };
                ammoByConfig[activeConfig] = st;
            }
            currentAmmo = st.inMag;
            reserveAmmo = st.reserve;
            UpdateHUD();
        }

        if (applyImmediately && activeConfig == null)
        {
            ammoUI?.Clear();
        }
    }

    WeaponConfig FindActiveConfig()
    {
        if (allConfigs == null || allConfigs.Length == 0) return null;

        // 1) via WeaponSwitcher.GetActiveWeapon() se existir
        if (weaponSwitcher != null)
        {
            var mi = weaponSwitcher.GetType().GetMethod("GetActiveWeapon",
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null)
            {
                var go = mi.Invoke(weaponSwitcher, null) as GameObject;
                if (go) return go.GetComponent<WeaponConfig>();
            }
        }

        // 2) primeira arma ativa com config
        foreach (var cfg in allConfigs)
            if (cfg && cfg.gameObject.activeInHierarchy)
                return cfg;

        return null; 
    }
}