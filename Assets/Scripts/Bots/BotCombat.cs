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
    // TWEAK: Aumentado de 40 para 60 para tiros mais rápidos e letais
    public float bulletSpeed = 60f; 

    [Header("Dificuldade - Curva de Mira (NOVO)")]
    [Tooltip("Define a imprecisão baseada na distância. Eixo X = Distância (m), Eixo Y = Erro (m).")]
    public AnimationCurve spreadOverDistance = new AnimationCurve(
        new Keyframe(0f, 0.1f),    // 0m: Quase perfeito
        new Keyframe(20f, 0.5f),   // 20m: Pequeno erro
        new Keyframe(50f, 2.5f),   // 50m: Erro médio
        new Keyframe(100f, 6.0f)   // 100m: Muito impreciso
    );

    [Header("Dificuldade / Nerf")]
    [Tooltip("Multiplicador final da curva. 1 = Normal, 0.5 = Sniper, 2 = Stormtrooper")]
    public float aimInaccuracyMultiplier = 1.0f;

    [Header("Arma: Rifle (Variáveis Finais)")]
    public int rifleMagSize = 30;
    public int rifleReserveAmmo = 120; // Aumentado
    public float rifleFireRate = 8f;   // Aumentado (era 6)
    public float rifleReloadTime = 2.2f;
    public float rifleDamage = 12f;    // Ajustado para combate sustentado

    [Header("Arma: Pistola (Variáveis Finais)")]
    public int pistolMagSize = 12;
    public int pistolReserveAmmo = 48;
    public float pistolFireRate = 3f;
    public float pistolReloadTime = 1.2f;
    public float pistolDamage = 18f;   // Dano alto por tiro

    [Header("Geral")]
    public float maxShootDistance = 150f; // Reduzido de 200f para forçar combates mais próximos
    public bool drawDebugRays = false;    // Desligado por defeito (Otimização)

    [Header("Dificuldade - Previsão")]
    [Range(0f, 1f)]
    public float leadAccuracy = 0.9f; // Ligeira imperfeição na previsão de movimento

    [Header("Debug - Diagnóstico ShootPoint")]
    public bool showShootPointGizmos = true;
    public bool logShootingPosition = false;

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
        myHealth = GetComponent<Health>();
        rifleMag = rifleMagSize;
        rifleRes = rifleReserveAmmo;
        pistolMag = pistolMagSize;
        pistolRes = pistolReserveAmmo;
        if (!eyes) eyes = shootPoint != null ? shootPoint : transform;
    }

    void LateUpdate()
    {
        if (!IsServer) return;

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
        targetRbCache = currentTarget ? currentTarget.GetComponent<Rigidbody>() : null;
        targetCcCache = currentTarget ? currentTarget.GetComponent<CharacterController>() : null;
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

        Vector3 origin = shootPoint.position;
        float dist = Vector3.Distance(origin, currentTarget.position);

        if (dist > maxShootDistance) return;

        // Verifica linha de tiro (Otimizado: só verifica quando vai disparar)
        Vector3 targetCenter = currentTarget.position + Vector3.up * 1.2f; // Aponta ao peito
        Vector3 directionToTarget = (targetCenter - origin).normalized;

        if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, dist, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            if (drawDebugRays) Debug.DrawLine(origin, hit.point, Color.black, 0.5f);
            return; // Bloqueado
        }

        // --- CÁLCULO DA NOVA MIRA DINÂMICA ---
        
        // 1. Previsão de movimento (Lead)
        Vector3 targetVelocity = targetRbCache != null ? targetRbCache.linearVelocity : Vector3.zero;
        float timeToHit = dist / bulletSpeed;
        Vector3 futurePos = currentTarget.position + targetVelocity * timeToHit * leadAccuracy;
        Vector3 perfectTargetPos = futurePos + Vector3.up * 1.3f; // Compensação de altura (Cabeça/Peito)

        // 2. Aplicação da Curva de Erro
        float spreadRadius = spreadOverDistance.Evaluate(dist) * aimInaccuracyMultiplier;
        
        // Adiciona erro aleatório dentro de uma esfera com o raio definido pela curva
        Vector3 errorOffset = Random.insideUnitSphere * spreadRadius;

        Vector3 noisyTargetPos = perfectTargetPos + errorOffset;
        Vector3 dir = (noisyTargetPos - origin).normalized;

        // Rotação do bot para o alvo (suavizada)
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), Time.deltaTime * 20f);

        if (logShootingPosition)
            Debug.Log($"[BotCombat] {gameObject.name}: Disparando. Spread: {spreadRadius:F2}m para Distância: {dist:F1}m");

        FireBullet(origin, dir);

        ConsumeAmmo();
        
        // Pequena variação no cooldown para parecer orgânico (human-like)
        float baseCooldown = 1f / GetCurrentFireRate();
        fireCooldown = baseCooldown * Random.Range(0.92f, 1.08f);
    }

    void FireBullet(Vector3 origin, Vector3 direction)
    {
        if (logShootingPosition) Debug.Log($"[DEBUG TIRO] Origem: {origin} | ShootPoint: {shootPoint.position}");
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));
        var bp = bullet.GetComponent<BulletProjectile>();
        var rb = bullet.GetComponent<Rigidbody>();
        var netObj = bullet.GetComponent<NetworkObject>();

        if (bp != null)
        {
            bp.damage = (currentWeapon == WeaponSlot.Rifle) ? rifleDamage : pistolDamage;
            bp.ownerTeam = -2; // Team Bot
            bp.ownerRoot = transform.root;
            bp.ownerClientId = ulong.MaxValue;
            bp.initialVelocity.Value = direction * bulletSpeed;
        }

        if (rb != null)
            rb.linearVelocity = direction * bulletSpeed;

        if (netObj != null)
            netObj.Spawn(true);
    }

    void EnsureUsableWeapon()
    {
        if (GetCurrentMag() <= 0 && GetCurrentReserve() <= 0)
        {
            WeaponSlot other = (currentWeapon == WeaponSlot.Rifle) ? WeaponSlot.Pistol : WeaponSlot.Rifle;
            if (GetTotalAmmo(other) > 0)
                currentWeapon = other;
        }
    }

    void TryTacticalReload()
    {
        if (GetCurrentReserve() > 0 && GetCurrentMag() < GetCurrentMagSize())
            StartReload();
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
        int mag = GetCurrentMag();
        int reserve = GetCurrentReserve();
        int needed = GetCurrentMagSize() - mag;
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

    void SetCurrentMag(int v)
    {
        if (currentWeapon == WeaponSlot.Rifle)
            rifleMag = v;
        else
            pistolMag = v;
    }

    int GetCurrentReserve() => (currentWeapon == WeaponSlot.Rifle) ? rifleRes : pistolRes;

    void SetCurrentReserve(int v)
    {
        if (currentWeapon == WeaponSlot.Rifle)
            rifleRes = v;
        else
            pistolRes = v;
    }

    int GetTotalAmmo(WeaponSlot s) => (s == WeaponSlot.Rifle) ? (rifleMag + rifleRes) : (pistolMag + pistolRes);

    void OnDrawGizmos()
    {
        if (!showShootPointGizmos) return;
        if (shootPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(shootPoint.position, 0.1f);
            Gizmos.DrawRay(shootPoint.position, shootPoint.forward * 2f);
        }
        if (eyes != null && eyes != shootPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(eyes.position, 0.08f);
        }
    }
}