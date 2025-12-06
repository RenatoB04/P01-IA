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
    [Tooltip("Quanto maior, pior a mira. 0 = Sniper Perfeito. 1.5 = Soldado Normal. 3.0 = Stormtrooper.")]
    public float aimInaccuracy = 1.5f; // <-- O SEGREDO DO NERF

    [Header("Arma: Rifle")]
    public int rifleMagSize = 30;
    public int rifleReserveAmmo = 90;
    public float rifleFireRate = 6f;    // Reduzi de 10 para 6 (mais lento)
    public float rifleReloadTime = 2.0f; // Aumentei reload
    public float rifleDamage = 5f;      // Reduzi de 10 para 5

    [Header("Arma: Pistola")]
    public int pistolMagSize = 12;
    public int pistolReserveAmmo = 48;
    public float pistolFireRate = 2f;   // Reduzi de 3 para 2
    public float pistolReloadTime = 1.5f;
    public float pistolDamage = 8f;     // Reduzi de 12 para 8

    [Header("Geral")]
    public float maxShootDistance = 200f;
    public bool drawDebugRays = false;

    [Header("Dificuldade - Previsão")]
    [Tooltip("0 = Não prevê movimento. 1 = Prevê movimento perfeito.")]
    [Range(0f, 1f)]
    public float leadAccuracy = 1.0f;

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
        
        rifleMag = rifleMagSize;
        rifleRes = rifleReserveAmmo;
        pistolMag = pistolMagSize;
        pistolRes = pistolReserveAmmo;
    }

    void Update()
    {
        if (!IsServer) return;

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

    public void SetInCombat(bool value)
    {
        inCombat = value;
    }

    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }

    // --- Lógica de Tiro ---

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

        // --- CÁLCULO DO LEAD (Previsão) ---
        Vector3 targetVelocity = Vector3.zero;

        // Tenta obter a velocidade do Rigidbody ou CharacterController do alvo
        var targetRb = currentTarget.GetComponent<Rigidbody>();
        if (targetRb) targetVelocity = targetRb.linearVelocity; // Unity 6 usa linearVelocity, Unity antigo usa velocity
        else
        {
            var cc = currentTarget.GetComponent<CharacterController>();
            if (cc) targetVelocity = cc.velocity;
        }

        float dist = Vector3.Distance(origin, currentTarget.position);
        float timeToHit = dist / bulletSpeed; // Tempo que a bala demora a chegar

        // Posição futura prevista (com fator de ajuste leadAccuracy)
        Vector3 futurePos = currentTarget.position + (targetVelocity * timeToHit * leadAccuracy);

        // Ponto ideal (Cabeça/Peito) na posição futura
        Vector3 perfectTargetPos = futurePos + Vector3.up * 1.2f;

        // --- CÁLCULO DO SPREAD (Espalhamento/Erro) ---
        // O erro aumenta com a distância
        float currentInaccuracy = aimInaccuracy + (dist * 0.01f);
        Vector3 errorOffset = Random.insideUnitSphere * currentInaccuracy;

        // Ponto final com Lead + Spread
        Vector3 noisyTargetPos = perfectTargetPos + errorOffset;
        Vector3 dir = (noisyTargetPos - origin).normalized;

        // Visualmente rodar para o alvo
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), Time.deltaTime * 8f);
        }

        if (drawDebugRays)
        {
            Debug.DrawLine(origin, currentTarget.position, Color.yellow, 0.1f); // Onde ele está
            Debug.DrawLine(origin, perfectTargetPos, Color.green, 0.1f);      // Onde vai estar (Lead)
            Debug.DrawRay(origin, dir * maxShootDistance, Color.red, 0.1f);   // Tiro real (Lead + Spread)
        }

        // --- DISPARAR (NETCODE) ---
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
            else
            {
                Debug.LogError($"[BotCombat] Erro no Prefab da Bala!");
                Destroy(bullet);
            }
        }

        ConsumeAmmo();
        fireCooldown = 1f / GetCurrentFireRate();
    }

    // --- Gestão de Munição e Armas ---

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