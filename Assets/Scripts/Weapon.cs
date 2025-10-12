using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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
    float nextFire;
    CharacterController playerCC;

    // ---- AMMO/RELOAD (apenas para Player) ----
    class AmmoState { public int inMag; public int reserve; }
    readonly Dictionary<WeaponConfig, AmmoState> ammoByConfig = new();
    int currentAmmo, reserveAmmo;
    bool isReloading;

    void Awake()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        playerCC = GetComponentInParent<CharacterController>();

        // ⚙️ detectar bot e libertar de configs
        if (GetComponentInParent<BotCombat>() != null)
            requireConfigForFire = false;

        allConfigs = GetComponentsInChildren<WeaponConfig>(true);
        weaponSwitcher = GetComponent<WeaponSwitcher>();   // opcional

        RefreshActiveConfig(applyImmediately: true);
        UpdateHUD();
    }

    void OnEnable()
    {
        if (shootAction) shootAction.action.Enable();
        if (reloadAction) reloadAction.action.Enable();
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
    }

    void Update()
    {
        RefreshActiveConfig(applyImmediately: true);

        // Player sem config (ex.: knife) -> não dispara
        if (requireConfigForFire && activeConfig == null) return;

        // RELOAD input (só interessa ao player)
        if (requireConfigForFire && reloadAction && reloadAction.action.WasPressedThisFrame())
        {
            TryReload();
        }

        bool automatic = activeConfig ? activeConfig.automatic : false;
        float useFireRate = activeConfig ? activeConfig.fireRate : fireRate;

        bool wantsShoot = automatic
            ? (shootAction && shootAction.action.IsPressed())
            : (shootAction && shootAction.action.WasPressedThisFrame());

        if (!wantsShoot || Time.time < nextFire) return;

        // Gate de munição (apenas player)
        if (requireConfigForFire)
        {
            if (currentAmmo <= 0)
            {
                if (fireAudio && activeConfig && activeConfig.emptyClickSfx)
                    fireAudio.PlayOneShot(activeConfig.emptyClickSfx);
                TryReload();
                return;
            }
            currentAmmo--;
        }

        Shoot();
        nextFire = Time.time + useFireRate;

        if (requireConfigForFire)
        {
            UpdateHUD();
            // auto-reload quando esvazia o carregador e há reserva
            if (currentAmmo == 0 && reserveAmmo > 0) TryReload();
        }
    }

    // Chamado pelos bots
    public void ShootExternally()
    {
        // Player sem config não dispara; Bot ignora esta regra
        if (requireConfigForFire && activeConfig == null) return;

        float useFireRate = activeConfig ? activeConfig.fireRate : fireRate;
        if (Time.time >= nextFire)
        {
            // Bots não gastam munição
            Shoot();
            nextFire = Time.time + useFireRate;
        }
    }

    void Shoot()
    {
        // Player sem config (knife) bloqueia disparo
        if (requireConfigForFire && activeConfig == null) return;

        // Seleciona fontes (config p/ player; locais p/ bot)
        Transform useFP = activeConfig ? activeConfig.firePoint : firePoint;
        GameObject useBullet = activeConfig ? activeConfig.bulletPrefab : bulletPrefab;
        ParticleSystem useMuzzle = activeConfig ? activeConfig.muzzleFlashPrefab : muzzleFlash;
        float useSpeed = activeConfig ? activeConfig.bulletSpeed : bulletSpeed;
        float useMaxDist = activeConfig ? activeConfig.maxAimDistance : maxAimDistance;

        if (!useBullet || !useFP) return;

        // direção (câmara quando existe; senão firePoint)
        Vector3 dir;
        Ray ray = new Ray(cam ? cam.position : useFP.position, cam ? cam.forward : useFP.forward);
        if (Physics.Raycast(ray, out var hit, useMaxDist, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - useFP.position).normalized;
        else
            dir = (ray.GetPoint(useMaxDist) - useFP.position).normalized;

        // bala
        var bullet = Instantiate(useBullet, useFP.position, Quaternion.LookRotation(dir));
        bullet.transform.position += dir * 0.2f;

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * useSpeed; // Unity 6

        // equipa / dono
        if (bullet.TryGetComponent<BulletProjectile>(out var bp))
        {
            var h = GetComponentInParent<Health>();
            if (h) bp.ownerTeam = h.team;
            bp.ownerRoot = h ? h.transform.root : transform.root;
        }

        // VFX
        if (useMuzzle)
        {
            var fx = Instantiate(useMuzzle, useFP.position, useFP.rotation, useFP);
            fx.Play();
            Destroy(fx.gameObject, 0.2f);
        }

        // SFX
        var fireClip = activeConfig ? activeConfig.fireSfx : null;
        if (fireAudio && fireClip) fireAudio.PlayOneShot(fireClip);
        else if (fireAudio && fireAudio.clip) fireAudio.PlayOneShot(fireAudio.clip);

        CrosshairUI.Instance?.Kick();
    }

    // ---------- AMMO / RELOAD ----------
    void TryReload()
    {
        if (!requireConfigForFire || activeConfig == null) return;
        if (isReloading) return;
        if (currentAmmo >= activeConfig.magSize) return;
        if (reserveAmmo <= 0) return;

        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (fireAudio && activeConfig && activeConfig.reloadSfx)
            fireAudio.PlayOneShot(activeConfig.reloadSfx);

        yield return new WaitForSeconds(activeConfig.reloadTime);

        int needed = activeConfig.magSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        isReloading = false;
        UpdateHUD();
    }

    void UpdateHUD()
    {
        if (!requireConfigForFire) return; // bots não usam HUD
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
        isReloading = false; // cancela reloads pendentes ao trocar

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

        // Se a arma ativa (player) não tiver config (ex.: Knife), garante que o HUD limpa
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
                if (go) return go.GetComponent<WeaponConfig>(); // pode ser null (ex.: Knife)
            }
        }

        // 2) primeira arma ativa com config
        foreach (var cfg in allConfigs)
            if (cfg && cfg.gameObject.activeInHierarchy)
                return cfg;

        return null; // sem config ativa (ex.: Knife) -> respeitado
    }
}
