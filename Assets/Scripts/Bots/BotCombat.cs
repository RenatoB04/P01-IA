using UnityEngine;
using Unity.Netcode; // <-- IMPORTANTE

/// <summary>
/// Lida com tiro, munições, reload e troca rifle/pistola para o bot.
/// AGORA é um NetworkBehaviour para poder spawnar balas de rede.
/// </summary>
public class BotCombat : NetworkBehaviour // <-- MODIFICADO
{
    [Header("Refs")]
    public Transform shootPoint;      // ponta da arma
    public Transform eyes;           // ponto para mirar (se null, usa shootPoint ou transform)
    public string playerTag = "Player";

    [Tooltip("Layer do jogador (usado para verificar o hit).")]
    public LayerMask playerLayer;

    [Tooltip("Layers de obstáculos (paredes, chão, etc.) que bloqueiam o tiro.")]
    public LayerMask obstacleLayer;

    [Header("Projectile (Netcode)")]
    [Tooltip("Prefab da bala de rede (o que tem Bullet.cs).")]
    public GameObject bulletPrefab; // <-- Arrastar o teu Prefab "Bullet"
    [Tooltip("Velocidade do prefab da bala.")]
    public float bulletSpeed = 40f;

    [Header("Rifle")]
    public int rifleMagSize = 30;
    public int rifleReserveAmmo = 90;
    public float rifleFireRate = 10f;
    public float rifleReloadTime = 1.5f;
    public float rifleDamage = 10f;

    [Header("Pistola")]
    public int pistolMagSize = 12;
    public int pistolReserveAmmo = 48;
    public float pistolFireRate = 3f;
    public float pistolReloadTime = 1.2f;
    public float pistolDamage = 12f;

    [Header("Geral")]
    public float maxShootDistance = 200f;
    public bool drawDebugRays = false;

    // Exposto para a AI
    public float AmmoNormalized
    {
        get
        {
            float curTotal = rifleMag + rifleRes + pistolMag + pistolRes;
            float maxTotal = rifleMagSize + rifleReserveAmmo + pistolMagSize + pistolReserveAmmo;
            if (maxTotal <= 0f) return 0f;
            return Mathf.Clamp01(curTotal / maxTotal);
        }
    }

    Transform player;
    bool inCombat = false;
    enum WeaponSlot { Rifle, Pistol }
    WeaponSlot currentWeapon = WeaponSlot.Rifle;
    int rifleMag, rifleRes, pistolMag, pistolRes;
    bool isReloading = false;
    float reloadTimer = 0f;
    float fireCooldown = 0f;
    LayerMask shootMask;

    void Awake()
    {
        if (!eyes) eyes = shootPoint != null ? shootPoint : transform;
        rifleMag = rifleMagSize;
        rifleRes = rifleReserveAmmo;
        pistolMag = pistolMagSize;
        pistolRes = pistolReserveAmmo;
        shootMask = playerLayer | obstacleLayer;
    }

    void Start()
    {
        if (!player && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }
    }

    void Update()
    {
        // Só o servidor (Host) pode controlar a lógica do bot
        if (!IsServer) return;

        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }

        fireCooldown -= Time.deltaTime;
        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f) FinishReload();
            return;
        }

        if (!inCombat) TryTacticalReload();
        if (inCombat && player) TryShootAtPlayer();
    }

    public void SetInCombat(bool value)
    {
        inCombat = value;
    }

    // --- LÓGICA DE TIRO MODIFICADA PARA NETCODE ---
    void TryShootAtPlayer()
    {
        if (!player || !IsServer || fireCooldown > 0f) return;

        EnsureUsableWeapon();

        if (GetCurrentMag() <= 0 && GetCurrentReserve() <= 0) return;
        if (GetCurrentMag() <= 0 && GetCurrentReserve() > 0)
        {
            StartReload();
            return;
        }

        Vector3 origin = shootPoint ? shootPoint.position : eyes.position;
        Vector3 targetPos = player.position + Vector3.up * 1.1f;
        Vector3 dir = (targetPos - origin).normalized;

        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.001f)
        {
            Quaternion wantRot = Quaternion.LookRotation(flatDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, Time.deltaTime * 10f);
        }

        if (drawDebugRays)
            Debug.DrawRay(origin, dir * maxShootDistance, Color.red, 0.1f);

        // Dispara a bala de rede (o teu Bullet.cs / BulletProjectile)
        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(dir));

            var bp = bullet.GetComponent<BulletProjectile>();
            var rb = bullet.GetComponent<Rigidbody>();
            var netObj = bullet.GetComponent<NetworkObject>();

            if (bp != null && rb != null && netObj != null)
            {
                // Configura o script BulletProjectile
                bp.damage = (currentWeapon == WeaponSlot.Rifle) ? rifleDamage : pistolDamage;
                bp.ownerTeam = -2; // -2 = Equipa "Bot"
                bp.ownerRoot = transform.root;

                // ----- LINHA CORRIGIDA -----
                bp.ownerClientId = ulong.MaxValue; // Usa a classe, não a instância

                // Define velocidade e sincroniza
                rb.linearVelocity = dir * bulletSpeed;
                bp.initialVelocity.Value = rb.linearVelocity;

                // Spawna a bala na rede
                netObj.Spawn(true);
            }
            else
            {
                Debug.LogError($"[BotCombat] bulletPrefab está mal configurado. Falta BulletProjectile (Bullet.cs), Rigidbody ou NetworkObject.");
                Destroy(bullet);
            }
        }
        else
        {
            Debug.LogWarning("[BotCombat] bulletPrefab é nulo.");
        }

        ConsumeAmmo();
        fireCooldown = 1f / GetCurrentFireRate();
    }

    // --- O resto dos métodos (Reload, Ammo, etc.) ---
    void EnsureUsableWeapon()
    {
        if (GetCurrentMag() <= 0 && GetCurrentReserve() <= 0)
        {
            WeaponSlot other = (currentWeapon == WeaponSlot.Rifle) ? WeaponSlot.Pistol : WeaponSlot.Rifle;
            if (GetTotalAmmo(other) > 0) currentWeapon = other;
        }
    }
    void TryTacticalReload()
    {
        if (GetCurrentReserve() > 0 && GetCurrentMag() < GetCurrentMagSize()) StartReload();
    }
    void StartReload()
    {
        if (isReloading || GetCurrentReserve() <= 0) return;
        isReloading = true;
        reloadTimer = (currentWeapon == WeaponSlot.Rifle) ? rifleReloadTime : pistolReloadTime;
    }
    void FinishReload()
    {
        isReloading = false;
        int magSize = GetCurrentMagSize();
        int mag = GetCurrentMag();
        int reserve = GetCurrentReserve();
        int needed = magSize - mag;
        int toLoad = Mathf.Min(needed, reserve);
        mag += toLoad;
        reserve -= toLoad;
        SetCurrentMag(mag);
        SetCurrentReserve(reserve);
    }
    void ConsumeAmmo() => SetCurrentMag(GetCurrentMag() - 1);
    float GetCurrentFireRate() => (currentWeapon == WeaponSlot.Rifle) ? rifleFireRate : pistolFireRate;
    int GetCurrentMagSize() => (currentWeapon == WeaponSlot.Rifle) ? rifleMagSize : pistolMagSize;
    int GetCurrentMag() => (currentWeapon == WeaponSlot.Rifle) ? rifleMag : pistolMag;
    void SetCurrentMag(int v) { if (currentWeapon == WeaponSlot.Rifle) rifleMag = v; else pistolMag = v; }
    int GetCurrentReserve() => (currentWeapon == WeaponSlot.Rifle) ? rifleRes : pistolRes;
    void SetCurrentReserve(int v) { if (currentWeapon == WeaponSlot.Rifle) rifleRes = v; else pistolRes = v; }
    int GetTotalAmmo(WeaponSlot s) => (s == WeaponSlot.Rifle) ? (rifleMag + rifleRes) : (pistolMag + pistolRes);
}