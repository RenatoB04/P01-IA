using UnityEngine;
using Unity.Netcode;

public class BotCombat : NetworkBehaviour
{
    [Header("Referências")]
    public Transform shootPoint;
    public Transform eyes;
    public string playerTag = "Player";

    [Header("Física e Layers")]
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Projétil (Netcode)")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;

    [Header("Dificuldade / Nerf")]
    [Tooltip("Erro de mira quando o jogador se mexe.")]
    public float aimInaccuracy = 1.5f;

    [Header("Arma: Rifle")]
    public int rifleMagSize = 30;
    public int rifleReserveAmmo = 90;
    public float rifleFireRate = 6f;
    public float rifleReloadTime = 2.0f;
    public float rifleDamage = 5f;

    [Header("Arma: Pistola")]
    public int pistolMagSize = 12;
    public int pistolReserveAmmo = 48;
    public float pistolFireRate = 2f;
    public float pistolReloadTime = 1.5f;
    public float pistolDamage = 8f;

    [Header("Geral")]
    public float maxShootDistance = 200f;
    public bool drawDebugRays = true;

    [Header("Dificuldade - Previsão")]
    [Range(0f, 1f)]
    public float leadAccuracy = 1.0f;

    // Propriedade que o BotAI precisa de ler (Isto resolve o erro CS1061)
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

    // --- Estado Interno ---
    private Transform currentTarget;
    private Rigidbody targetRbCache;
    private CharacterController targetCcCache;
    private Health myHealth;
    private bool inCombat = false;

    private enum WeaponSlot { Rifle, Pistol }
    private WeaponSlot currentWeapon = WeaponSlot.Rifle;

    private int rifleMag, rifleRes, pistolMag, pistolRes;
    private bool isReloading = false;
    private float reloadTimer = 0f;
    private float fireCooldown = 0f;

    void Awake()
    {
        if (!eyes) eyes = shootPoint != null ? shootPoint : transform;
        myHealth = GetComponent<Health>();

        rifleMag = rifleMagSize;
        rifleRes = rifleReserveAmmo;
        pistolMag = pistolMagSize;
        pistolRes = pistolReserveAmmo;
    }

    void Update()
    {
        if (!IsServer) return;

        // --- CORREÇÃO DO ERRO ---
        // Adicionei .Value porque currentHealth é uma NetworkVariable
        if (myHealth != null && myHealth.currentHealth.Value <= 0) return;

        if (fireCooldown > 0f) fireCooldown -= Time.deltaTime;

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f) FinishReload();
            return;
        }

        if (!inCombat)
        {
            TryTacticalReload();
        }
        else if (currentTarget != null)
        {
            TryShootAtTarget();
        }
    }

    public void SetInCombat(bool value) => inCombat = value;

    public void SetTarget(Transform target)
    {
        if (currentTarget == target) return;
        currentTarget = target;

        if (currentTarget != null)
        {
            targetRbCache = currentTarget.GetComponent<Rigidbody>();
            targetCcCache = currentTarget.GetComponent<CharacterController>();
        }
        else
        {
            targetRbCache = null;
            targetCcCache = null;
        }
    }

    void TryShootAtTarget()
    {
        if (fireCooldown > 0f) return;

        EnsureUsableWeapon();
        if (GetCurrentMag() <= 0 && GetCurrentReserve() <= 0) return;
        if (GetCurrentMag() <= 0 && GetCurrentReserve() > 0)
        {
            StartReload();
            return;
        }

        Vector3 origin = shootPoint ? shootPoint.position : eyes.position;
        float dist = Vector3.Distance(origin, currentTarget.position);

        if (dist > maxShootDistance) return;

        // --- PAREDES (RAYCAST) ---
        Vector3 directionToTarget = (currentTarget.position + Vector3.up) - origin;
        if (Physics.Raycast(origin, directionToTarget.normalized, out RaycastHit hit, dist, obstacleLayer))
        {
            if (drawDebugRays) Debug.DrawLine(origin, hit.point, Color.black, 0.5f);
            return;
        }

        // --- CÁLCULOS DE TIRO (LEAD + SPREAD) ---
        Vector3 targetVelocity = Vector3.zero;
        if (targetRbCache != null) targetVelocity = targetRbCache.linearVelocity;
        else if (targetCcCache != null) targetVelocity = targetCcCache.velocity;

        float timeToHit = dist / bulletSpeed;
        Vector3 futurePos = currentTarget.position + (targetVelocity * timeToHit * leadAccuracy);
        Vector3 perfectTargetPos = futurePos + Vector3.up * 1.3f;

        float targetSpeed = targetVelocity.magnitude;
        float currentSpreadBase = (targetSpeed < 0.2f) ? aimInaccuracy * 0.1f : aimInaccuracy;
        float finalInaccuracy = currentSpreadBase + (dist * 0.015f);
        Vector3 errorOffset = Random.insideUnitSphere * finalInaccuracy;

        Vector3 noisyTargetPos = perfectTargetPos + errorOffset;
        Vector3 dir = (noisyTargetPos - origin).normalized;

        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), Time.deltaTime * 15f);

        if (drawDebugRays)
        {
            Debug.DrawLine(origin, perfectTargetPos, Color.green, 0.1f);
            Debug.DrawRay(origin, dir * maxShootDistance, Color.red, 0.1f);
        }

        // DISPARAR
        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(dir));
            var bp = bullet.GetComponent<BulletProjectile>();
            var rb = bullet.GetComponent<Rigidbody>();
            var netObj = bullet.GetComponent<NetworkObject>();

            if (bp && rb && netObj)
            {
                bp.damage = (currentWeapon == WeaponSlot.Rifle) ? rifleDamage : pistolDamage;
                bp.ownerTeam = -2;
                bp.ownerRoot = transform.root;
                bp.ownerClientId = ulong.MaxValue;

                rb.linearVelocity = dir * bulletSpeed;
                bp.initialVelocity.Value = rb.linearVelocity;

                netObj.Spawn(true);
            }
            else if (netObj)
            {
                netObj.Spawn(true);
            }
        }

        ConsumeAmmo();
        float baseCooldown = 1f / GetCurrentFireRate();
        fireCooldown = baseCooldown * Random.Range(0.95f, 1.05f);
    }

    // --- Helpers ---
    void EnsureUsableWeapon() { if (GetCurrentMag() <= 0 && GetCurrentReserve() <= 0) { WeaponSlot other = (currentWeapon == WeaponSlot.Rifle) ? WeaponSlot.Pistol : WeaponSlot.Rifle; if (GetTotalAmmo(other) > 0) currentWeapon = other; } }
    void TryTacticalReload() { if (GetCurrentReserve() > 0 && GetCurrentMag() < GetCurrentMagSize()) StartReload(); }
    void StartReload() { if (isReloading || GetCurrentReserve() <= 0) return; isReloading = true; reloadTimer = (currentWeapon == WeaponSlot.Rifle) ? rifleReloadTime : pistolReloadTime; }
    void FinishReload() { isReloading = false; int mag = GetCurrentMag(); int reserve = GetCurrentReserve(); int needed = GetCurrentMagSize() - mag; int toLoad = Mathf.Min(needed, reserve); mag += toLoad; reserve -= toLoad; SetCurrentMag(mag); SetCurrentReserve(reserve); }
    void ConsumeAmmo() => SetCurrentMag(GetCurrentMag() - 1);
    float GetCurrentFireRate() => (currentWeapon == WeaponSlot.Rifle) ? rifleFireRate : pistolFireRate;
    int GetCurrentMagSize() => (currentWeapon == WeaponSlot.Rifle) ? rifleMagSize : pistolMagSize;
    int GetCurrentMag() => (currentWeapon == WeaponSlot.Rifle) ? rifleMag : pistolMag;
    void SetCurrentMag(int v) { if (currentWeapon == WeaponSlot.Rifle) rifleMag = v; else pistolMag = v; }
    int GetCurrentReserve() => (currentWeapon == WeaponSlot.Rifle) ? rifleRes : pistolRes;
    void SetCurrentReserve(int v) { if (currentWeapon == WeaponSlot.Rifle) rifleRes = v; else pistolRes = v; }
    int GetTotalAmmo(WeaponSlot s) => (s == WeaponSlot.Rifle) ? (rifleMag + rifleRes) : (pistolMag + pistolRes);
}