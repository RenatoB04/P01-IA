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

    [Header("Input (apenas jogador)")]
    [SerializeField] InputActionReference shootAction;
    [SerializeField] InputActionReference reloadAction;

    [Header("Settings (fallbacks se não houver config)")]
    [SerializeField] float bulletSpeed = 40f;
    [SerializeField] float fireRate = 0.12f;
    [SerializeField] float maxAimDistance = 200f;

    [Header("Comportamento")]
    [Tooltip("TRUE = arma usa munição/reload. Deve ser TRUE no Player e também nos Bots se queres limitar balas.")]
    [SerializeField] bool requireConfigForFire = true;

    [Header("HUD")]
    [SerializeField] AmmoUI ammoUI;  // HUD do player. Bots podem deixar isto a null.

    // ---- Auto-config ----
    WeaponConfig[] allConfigs;
    WeaponConfig activeConfig;
    Component weaponSwitcher;

    // ---- Estado de tiro geral ----
    float nextFireTimeUnscaled;
    CharacterController playerCC;

    // ---- AMMO/RELOAD ----
    class AmmoState { public int inMag; public int reserve; }
    readonly Dictionary<WeaponConfig, AmmoState> ammoByConfig = new();
    int currentAmmo, reserveAmmo;
    bool isReloading;

    // ---- INPUT IA ----
    bool aiWantsShoot; // controlado pelo bot

    // ====== CICLO DE VIDA ======
    void Awake()
    {
        // Garantir referência da câmara
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

        allConfigs = GetComponentsInChildren<WeaponConfig>(true);
        weaponSwitcher = GetComponent<WeaponSwitcher>();

        RefreshActiveConfig(applyImmediately: true);
        UpdateHUD();
    }

    void OnEnable()
    {
        // Activar input do player (bots podem não ter isto ligado, não faz mal)
        if (shootAction) shootAction.action.Enable();
        if (reloadAction) reloadAction.action.Enable();

        // Reset de emergência
        ResetWeaponState();
    }

    void OnDisable()
    {
        // guardar munição actual no dicionário quando a arma deixa de estar activa
        if (requireConfigForFire && activeConfig && ammoByConfig.ContainsKey(activeConfig))
        {
            ammoByConfig[activeConfig].inMag = currentAmmo;
            ammoByConfig[activeConfig].reserve = reserveAmmo;
        }

        // desligar inputs
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

    // ====== UPDATE PRINCIPAL (Player + Bot) ======
    void Update()
    {
        // garantir config activa
        if (activeConfig == null)
            RefreshActiveConfig(applyImmediately: true);

        if (requireConfigForFire && activeConfig == null) return;

        // garantir input ligado (só interessa ao player)
        if (shootAction != null && !shootAction.action.enabled)
        {
            shootAction.action.Enable();
        }

        // 1) INPUT DE RELOAD MANUAL (só faz sentido se houver munição)
        if (requireConfigForFire && reloadAction && reloadAction.action.WasPressedThisFrame())
        {
            TryReload();
        }

        // 2) AUTO-RELOAD SE A ARMA ESTÁ VAZIA MAS TEM RESERVA
        if (requireConfigForFire && currentAmmo <= 0 && reserveAmmo > 0 && !isReloading)
        {
            TryReload();
        }

        // bloquear tiro durante reload
        if (isReloading) return;

        // --- INPUT DE DISPARO (Player OU Bot) ---
        bool wantsShoot = false;

        // Jogador
        if (shootAction != null)
        {
            bool automatic = activeConfig ? activeConfig.automatic : false;
            wantsShoot = automatic
                ? shootAction.action.IsPressed()
                : shootAction.action.WasPressedThisFrame();
        }

        // IA (só substitui se o jogador não carregou)
        if (!wantsShoot)
            wantsShoot = aiWantsShoot;

        if (!wantsShoot)
            return;

        // cooldown
        float useFireRate = activeConfig ? activeConfig.fireRate : fireRate;
        if (Time.unscaledTime < nextFireTimeUnscaled)
            return;

        // munição
        if (requireConfigForFire)
        {
            if (currentAmmo <= 0)
            {
                // tentar reload automático se houver reserva
                if (reserveAmmo > 0 && !isReloading)
                {
                    TryReload();
                }

                // ainda não há munição? faz click seco e sai
                if (currentAmmo <= 0 || isReloading)
                {
                    PlayEmptyClickIfPossible();
                    return;
                }
            }

            // consumir bala
            currentAmmo--;
        }

        // disparar
        Shoot();

        // cooldown
        nextFireTimeUnscaled = Time.unscaledTime + useFireRate;

        // HUD player
        if (requireConfigForFire)
        {
            UpdateHUD();

            // auto-reload se acabou o carregador
            if (currentAmmo == 0 && reserveAmmo > 0)
                TryReload();
        }
    }

    // ====== API PARA BOTS ======
    // O bot chama isto no seu próprio Update/Behaviour.
    public void SetAIWantsShoot(bool shoot)
    {
        aiWantsShoot = shoot;
    }

    // ====== TIRO REAL ======
    void Shoot()
    {
        if (requireConfigForFire && activeConfig == null) return;

        Transform useFP = activeConfig ? activeConfig.firePoint : firePoint;
        GameObject useBullet = activeConfig ? activeConfig.bulletPrefab : bulletPrefab;
        ParticleSystem useMuzzle = activeConfig ? activeConfig.muzzleFlashPrefab : muzzleFlash;
        float useSpeed = activeConfig ? activeConfig.bulletSpeed : bulletSpeed;
        float useMaxDist = activeConfig ? activeConfig.maxAimDistance : maxAimDistance;

        if (!useBullet || !useFP) return;

        // calcula direcção (raycast da câmara se existir)
        Vector3 dir;
        Ray ray = new Ray(cam ? cam.position : useFP.position, cam ? cam.forward : useFP.forward);
        if (Physics.Raycast(ray, out var hit, useMaxDist, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - useFP.position).normalized;
        else
            dir = (ray.GetPoint(useMaxDist) - useFP.position).normalized;

        // instancia projéctil
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

        // muzzle flash
        if (useMuzzle)
        {
            var fx = Instantiate(useMuzzle, useFP.position, useFP.rotation, useFP);
            fx.Play();
            Destroy(fx.gameObject, 0.2f);
        }

        // som de tiro (corrigido)
        var fireClip = activeConfig ? activeConfig.fireSfx : null;
        if (fireAudio)
        {
            if (fireClip)
                fireAudio.PlayOneShot(fireClip);
            else if (fireAudio.clip)
                fireAudio.PlayOneShot(fireAudio.clip);
        }

        // kick do crosshair (só faz efeito se houver UI)
        CrosshairUI.Instance?.Kick();
    }

    void PlayEmptyClickIfPossible()
    {
        if (fireAudio && activeConfig && activeConfig.emptyClickSfx)
            fireAudio.PlayOneShot(activeConfig.emptyClickSfx);
    }

    // ====== AMMO / RELOAD ======
    public void AddReserveAmmo(int amount)
    {
        if (!requireConfigForFire || activeConfig == null) return;
        if (amount <= 0) return;

        int bulletsToAdd = amount * activeConfig.magSize;
        reserveAmmo += bulletsToAdd;

        // sincronizar dicionário
        if (ammoByConfig.ContainsKey(activeConfig))
            ammoByConfig[activeConfig].reserve = reserveAmmo;

        UpdateHUD();

        // auto-reload se o carregador está vazio
        if (currentAmmo == 0)
            TryReload();
    }

    public void TryReload()
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

        // usar tempo não escalado para não quebrar em slow-mo
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

    // ====== TROCA / CONFIG DE ARMA ======
    public void SetActiveWeapon(GameObject weaponGO)
    {
        activeConfig = weaponGO ? weaponGO.GetComponent<WeaponConfig>() : null;
        RefreshActiveConfig(applyImmediately: true);
    }

    void RefreshActiveConfig(bool applyImmediately)
    {
        var newCfg = FindActiveConfig();

        if (newCfg == null)
        {
            // não há config válido → limpa HUD mas não rebenta
            if (applyImmediately)
            {
                activeConfig = null;
                ammoUI?.Clear();
            }
            return;
        }

        // se não mudou, sai
        if (newCfg == activeConfig) return;

        // troca
        activeConfig = newCfg;
        isReloading = false;

        if (applyImmediately && activeConfig != null)
        {
            // aplicar valores da arma
            firePoint = activeConfig.firePoint ?? firePoint;
            bulletPrefab = activeConfig.bulletPrefab ?? bulletPrefab;
            muzzleFlash = activeConfig.muzzleFlashPrefab ?? muzzleFlash;
            bulletSpeed = activeConfig.bulletSpeed;
            fireRate = activeConfig.fireRate;
            maxAimDistance = activeConfig.maxAimDistance;

            // inicializar/recuperar munição desta arma
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
            var mi = weaponSwitcher.GetType().GetMethod(
                "GetActiveWeapon",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (mi != null)
            {
                var go = mi.Invoke(weaponSwitcher, null) as GameObject;
                if (go) return go.GetComponent<WeaponConfig>();
            }
        }

        // 2) primeira arma activa encontrada na hierarquia
        foreach (var cfg in allConfigs)
            if (cfg && cfg.gameObject.activeInHierarchy)
                return cfg;

        return null;
    }

    // ====== GETTERS ÚTEIS ======
    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    public int GetReserveAmmo()
    {
        return reserveAmmo;
    }

    public bool IsCurrentlyReloading()
    {
        return isReloading;
    }

    public int GetActiveWeaponMagSize()
    {
        if (activeConfig != null)
            return activeConfig.magSize;
        return 0;
    }
}
