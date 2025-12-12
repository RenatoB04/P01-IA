using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static UnityEngine.Time;
using Unity.Netcode;
using System;


public class Weapon : NetworkBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform cam;
    [SerializeField] ParticleSystem muzzleFlash;
    [SerializeField] AudioSource fireAudio;

    [Header("Input")]
    [SerializeField] InputActionReference shootAction;
    [SerializeField] InputActionReference reloadAction;

    [Header("Settings (fallbacks se não houver config)")]
    [SerializeField] float bulletSpeed = 40f;
    [SerializeField] float fireRate = 0.12f;
    [SerializeField] float maxAimDistance = 200f;

    [Header("Behaviour")]
    [Tooltip("Player: TRUE (só dispara com WeaponConfig). Bot: FALSE (usa campos locais).")]
    [SerializeField] bool requireConfigForFire = true;

    
    WeaponConfig[] allConfigs;
    WeaponConfig activeConfig;
    Component weaponSwitcher;
    CharacterController playerCC;
    private bool isBot = false;
    private Health ownerHealth; 

    
    private PlayerShield playerShield;

    
    float nextTimeUnscaled;
    class AmmoState { public int inMag; public int reserve; }
    readonly Dictionary<WeaponConfig, AmmoState> ammoByConfig = new();
    int currentAmmo, reserveAmmo;
    bool isReloading;

    void Awake()
    {
        if (!cam)
        {
            if (FP_Controller_IS.PlayerCameraRoot != null) cam = FP_Controller_IS.PlayerCameraRoot;
            else if (Camera.main) cam = Camera.main.transform;
        }
        playerCC = GetComponentInParent<CharacterController>();

        
        ownerHealth = GetComponentInParent<Health>();

        
        playerShield = GetComponentInParent<PlayerShield>();

        if (ownerHealth == null && requireConfigForFire)
        {
            Debug.LogError($"Weapon.cs (Awake): Não foi possível encontrar o script 'Health' no pai. A bala não terá equipa.");
        }

        if (GetComponentInParent<BotCombat>() != null)
        {
            requireConfigForFire = false;
            isBot = true;
        }
        allConfigs = GetComponentsInChildren<WeaponConfig>(true);
        weaponSwitcher = GetComponent<WeaponSwitcher>();
    }

    void Start()
    {
        if (isBot)
        {
            EnableInputsAndHUD(true);
        }
    }

    
    public override void OnNetworkSpawn()
    {
        if (!IsOwner && !isBot)
        {
            EnableInputsAndHUD(false); 
            this.enabled = false; 
            return;
        }

        EnableInputsAndHUD(true);
    }

    void EnableInputsAndHUD(bool enabled)
    {
        if (enabled)
        {
            if (shootAction) shootAction.action.Enable();
            if (reloadAction) reloadAction.action.Enable();
            ResetWeaponState();
            RefreshActiveConfig(applyImmediately: true);
        }
        else
        {
            if (shootAction) shootAction.action.Disable();
            if (reloadAction) reloadAction.action.Disable();
        }
    }

    void OnDisable()
    {
        if (IsOwner || isBot)
        {
            if (requireConfigForFire && activeConfig && ammoByConfig.ContainsKey(activeConfig))
            {
                ammoByConfig[activeConfig].inMag = currentAmmo;
                ammoByConfig[activeConfig].reserve = reserveAmmo;
            }
            EnableInputsAndHUD(false);
            isReloading = false;
            StopAllCoroutines();
        }
    }

    public void ResetWeaponState()
    {
        nextTimeUnscaled = Time.unscaledTime;
        isReloading = false;
        StopAllCoroutines();
    }

    void Update()
    {
        RefreshActiveConfig(applyImmediately: true);

        
        
        
        
        if (requireConfigForFire)
            UpdateHUD();
        

        if (requireConfigForFire && activeConfig == null) return;

        
        bool isDead = ownerHealth && ownerHealth.isDead.Value;
        bool isPaused = PauseMenuManager.IsPaused;
        bool isShielded = playerShield && playerShield.IsShieldActive.Value; 

        if (isDead || isPaused || isShielded) 
        {
            if (shootAction && shootAction.action.enabled) shootAction.action.Disable();
            if (reloadAction && reloadAction.action.enabled) reloadAction.action.Disable();
            return;
        }
        else
        {
            if (shootAction != null && !shootAction.action.enabled) shootAction.action.Enable();
            if (reloadAction != null && !reloadAction.action.enabled) reloadAction.action.Enable();
        }
        

        if (requireConfigForFire && reloadAction && reloadAction.action.WasPressedThisFrame()) TryReload();
        if (requireConfigForFire && currentAmmo <= 0 && reserveAmmo > 0 && !isReloading) TryReload();
        if (isReloading) return;

        bool automatic = activeConfig ? activeConfig.automatic : false;
        float useFireRate = activeConfig ? activeConfig.fireRate : this.fireRate;
        bool wantsShoot = shootAction != null && (automatic ? shootAction.action.IsPressed() : shootAction.action.WasPressedThisFrame());

        if (!wantsShoot || Time.unscaledTime < nextTimeUnscaled) return;

        if (requireConfigForFire)
        {
            if (currentAmmo <= 0)
            {
                if (fireAudio && activeConfig && activeConfig.emptyClickSfx)
                    fireAudio.PlayOneShot(activeConfig.emptyClickSfx);
                return;
            }
            currentAmmo--;
        }

        Shoot();
        nextTimeUnscaled = Time.unscaledTime + useFireRate;
        if (requireConfigForFire)
        {
            
            
            UpdateHUD();
            if (currentAmmo == 0 && reserveAmmo > 0) TryReload();
        }
    }

    public void ShootExternally()
    {
        if (requireConfigForFire && activeConfig == null) return;
        if (ownerHealth && ownerHealth.isDead.Value) return;
        if (playerShield && playerShield.IsShieldActive.Value) return;

        float useFireRate = activeConfig ? activeConfig.fireRate : this.fireRate;
        if (Time.unscaledTime >= nextTimeUnscaled)
        {
            if (requireConfigForFire)
            {
                if (currentAmmo <= 0) return;
                currentAmmo--;
            }

            Shoot();
            nextTimeUnscaled = Time.unscaledTime + useFireRate;

            if (requireConfigForFire)
            {
                UpdateHUD();
                if (currentAmmo == 0 && reserveAmmo > 0) TryReload();
            }
        }
    }

    void Shoot()
    {
        if (requireConfigForFire && activeConfig == null) return;
        Transform useFP = activeConfig ? activeConfig.firePoint : firePoint;
        GameObject useBullet = activeConfig ? activeConfig.bulletPrefab : bulletPrefab;
        ParticleSystem useMuzzle = activeConfig ? activeConfig.muzzleFlashPrefab : muzzleFlash;
        float useSpeed = activeConfig ? activeConfig.bulletSpeed : this.bulletSpeed;
        float useMaxDist = activeConfig ? activeConfig.maxAimDistance : this.maxAimDistance;

        if (!useBullet || !useFP)
        {
            Debug.LogError($"{name}/Weapon.Shoot: firePoint ou bulletPrefab nulos. activeConfig={(activeConfig ? activeConfig.name : "null")}");
            return;
        }
        if (cam == null) cam = useFP;

        Vector3 dir;
        Ray ray = new Ray(cam.position, cam.forward);
        if (Physics.Raycast(ray, out var hit, useMaxDist, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - useFP.position).normalized;
        else
            dir = (ray.GetPoint(useMaxDist) - useFP.position).normalized;

        Vector3 spawnPos = useFP.position + dir * 0.2f;
        int shooterTeam = ownerHealth ? ownerHealth.team.Value : -1;
        ulong shooterClientId = IsOwner ? OwnerClientId : ulong.MaxValue;
        float speedToSend = useSpeed;

        SpawnBulletServerRpc(spawnPos, dir, speedToSend, shooterTeam, shooterClientId);

        if (useMuzzle)
        {
            var fx = Instantiate(useMuzzle, useFP.position, useFP.rotation, useFP);
            fx.Play();
            Destroy(fx.gameObject, 0.2f);
        }

        var fireClip = activeConfig ? activeConfig.fireSfx : null;
        if (fireAudio && fireClip) fireAudio.PlayOneShot(fireClip);
        else if (fireAudio && fireAudio.clip) fireAudio.PlayOneShot(fireAudio.clip);

        CrosshairUI.Instance?.Kick();
    }

    private GameObject ResolveBulletPrefabServer(out string reasonIfInvalid)
    {
        reasonIfInvalid = null;
        GameObject prefab = activeConfig && activeConfig.bulletPrefab ? activeConfig.bulletPrefab : bulletPrefab;
        if (prefab == null) { return null; }
        var rootNO = prefab.GetComponent<NetworkObject>();
        if (rootNO == null) { return null; }
        return prefab;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnBulletServerRpc(Vector3 position, Vector3 direction, float speed, int shooterTeam, ulong shooterClientId)
    {
        string invalidReason;
        var prefab = ResolveBulletPrefabServer(out invalidReason);
        if (prefab == null) { return; }

        var bullet = Instantiate(prefab, position, Quaternion.LookRotation(direction));
        if (bullet.TryGetComponent<Rigidbody>(out var rb)) rb.linearVelocity = direction * speed;
        if (bullet.TryGetComponent<BulletProjectile>(out var bp))
        {
            bp.ownerTeam = shooterTeam;
            bp.ownerRoot = transform.root;
            bp.ownerClientId = shooterClientId;
            bp.initialVelocity.Value = direction * speed;
        }

        var no = bullet.GetComponent<NetworkObject>();
        if (no != null)
        {
            try { no.Spawn(true); }
            catch (Exception ex) { Destroy(bullet); }
        }
        else { Destroy(bullet); }
    }

    public void AddReserveAmmo(int amount)
    {
        if (!requireConfigForFire || activeConfig == null || amount <= 0) return;
        reserveAmmo = Mathf.Max(0, reserveAmmo + amount);
        UpdateHUD();
        if (currentAmmo == 0) TryReload();
    }

    void TryReload()
    {
        if (!requireConfigForFire || activeConfig == null) return;
        if (isReloading || currentAmmo >= activeConfig.magSize || reserveAmmo <= 0) return;
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

    public void UpdateHUD()
    {
        
        

        
        
        if (requireConfigForFire && AmmoUI.Instance != null)
        {
            AmmoUI.Instance.Set(currentAmmo, reserveAmmo);
        }
    }

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
            firePoint = activeConfig.firePoint ?? firePoint;
            bulletPrefab = activeConfig.bulletPrefab ?? bulletPrefab;
            muzzleFlash = activeConfig.muzzleFlashPrefab ?? muzzleFlash;
            bulletSpeed = activeConfig.bulletSpeed;
            fireRate = activeConfig.fireRate;
            maxAimDistance = activeConfig.maxAimDistance;

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
            if (AmmoUI.Instance != null) AmmoUI.Instance.Clear();
        }
    }

    WeaponConfig FindActiveConfig()
    {
        if (allConfigs == null || allConfigs.Length == 0) return null;
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
        foreach (var cfg in allConfigs)
            if (cfg && cfg.gameObject.activeInHierarchy)
                return cfg;
        return null;
    }
}