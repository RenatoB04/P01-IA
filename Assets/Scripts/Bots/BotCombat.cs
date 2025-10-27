using UnityEngine;
using UnityEngine.AI; // Necessário para NavMeshAgent

// Obriga estes componentes a existir no mesmo GameObject
[RequireComponent(typeof(BotWeaponManager))]
[RequireComponent(typeof(Weapon))]
public class BotCombat : MonoBehaviour
{
    // ==========================
    // REFERÊNCIAS
    // ==========================

    [Header("Componentes (auto)")]
    [SerializeField] private BotWeaponManager weaponManager;
    [SerializeField] private Weapon weaponScript;

    [Header("Referências no pai")]
    [SerializeField] private BotAI_Proto botAI;
    private NavMeshAgent agent;

    [Header("Alvo")]
    [SerializeField] private Transform player;

    [Header("Pontos de Referência")]
    public Transform eyes; // de onde sai o raycast para ver o jogador

    [Header("Visão / Mira")]
    [Range(30f, 180f)] public float fovDegrees = 130f; // cone total (ex: 130º)
    [Range(1f, 45f)] public float maxShootAngle = 12f; // quão bem alinhado tem de estar para poder disparar
    public float aimHeightOffset = 1.1f; // mira ligeiramente acima do chão do player

    [Header("Combate")]
    public float attackRange = 25f;      // distância máxima para atacar
    public float stopDistance = 8f;      // distância confortável (se usares navmesh para parar)
    public float aimTurnSpeed = 360f;    // quão rápido roda para alinhar com o alvo

    [Header("Cooldowns de tiro")]
    public float rifleFireCooldown = 0.1f;
    public float pistolFireCooldown = 0.5f;

    [Header("Layers / Detecção")]
    public string playerTag = "Player"; // fallback
    public LayerMask playerLayer;       // layer do player
    public LayerMask obstacleMask;      // coisas que bloqueiam visão (não meter player aqui)

    [Header("Debug")]
    public bool debugLogs = false;
    public bool drawRays = true;

    // ==========================
    // ESTADO INTERNO
    // ==========================

    private float nextFireTime;
    private const int RIFLE_INDEX = 0;
    private const int PISTOL_INDEX = 1;
    private const int KNIFE_INDEX = 2; // reservado para futuro

    // guardamos se este frame decidimos disparar
    private bool wantsShootThisFrame;


    // ==========================
    // UNITY LIFECYCLE
    // ==========================

    void Awake()
    {
        // apanhar refs locais
        weaponManager = GetComponent<BotWeaponManager>();
        weaponScript = GetComponent<Weapon>();
        botAI = GetComponentInParent<BotAI_Proto>();
        agent = GetComponentInParent<NavMeshAgent>();

        // validar
        if (!weaponManager || !weaponScript || !botAI || !agent)
        {
            Debug.LogError($"[BotCombat] Faltam componentes essenciais em {gameObject.name} ou no pai!", this);
            enabled = false;
            return;
        }

        // tentar descobrir a layer "player" se não estiver preenchida
        if (playerLayer.value == 0)
        {
            int idx = LayerMask.NameToLayer("player");
            if (idx >= 0)
                playerLayer = 1 << idx;
        }

        // encontrar alvo
        FindPlayerTarget();

        // encontrar "eyes" se não tiver sido ligado no inspector
        if (!eyes)
        {
            Transform foundEyes = transform.Find("Eyes");
            eyes = foundEyes ? foundEyes : botAI.transform;
            if (debugLogs) Debug.Log($"[BotCombat] Eyes = {(eyes ? eyes.name : "null")}", this);
        }

        if (debugLogs)
            Debug.Log($"[BotCombat] Awake ok. Player: {(player ? player.name : "N/D")}", this);
    }


    void Update()
    {
        // vamos decidir se quer ou não disparar ESTE frame
        wantsShootThisFrame = false;

        // garantir que temos alvo
        if (!player || !botAI)
        {
            if (!player) FindPlayerTarget();
            ApplyShootIntentToWeapon(false); // parar gatilho
            return;
        }

        // se a arma está a recarregar, pausa (não disparamos enquanto reload)
        if (weaponScript.IsCurrentlyReloading())
        {
            ApplyShootIntentToWeapon(false);
            return;
        }

        // fora de combate → gestão de arma e reload, não disparamos
        if (!botAI.IsInCombatState())
        {
            HandleOutOfCombatLogic();
            ApplyShootIntentToWeapon(false);
            return;
        }

        // em combate → tentar disparar
        HandleCombatLogic();

        // depois de decidir lógica de combate, aplica a intenção à arma
        ApplyShootIntentToWeapon(wantsShootThisFrame);
    }


    // ==========================
    // LÓGICA PRINCIPAL
    // ==========================

    void HandleOutOfCombatLogic()
    {
        // Garantir rifle equipada
        if (weaponManager.GetCurrentWeaponIndex() != RIFLE_INDEX)
        {
            if (debugLogs) Debug.Log("[BotCombat] Fora de combate → equipar Rifle.");
            weaponManager.SelectWeapon(RIFLE_INDEX);
            return;
        }

        // Se já está com a rifle, tenta recarregar se estiver abaixo da capacidade
        int magSize = weaponScript.GetActiveWeaponMagSize();
        if (magSize > 0 &&
            weaponScript.GetCurrentAmmo() < magSize &&
            weaponScript.GetReserveAmmo() > 0)
        {
            if (debugLogs) Debug.Log("[BotCombat] Fora de combate → reload Rifle.");
            weaponScript.TryReload();
        }
    }


    void HandleCombatLogic()
    {
        // ponto onde queremos acertar (cabeça/peito do player)
        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;

        // distância ao alvo
        Vector3 toTarget = aimPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance > attackRange)
        {
            if (debugLogs) Debug.Log($"[BotCombat] Longe demais ({distance:F1}m > {attackRange}m).");
            // demasiado longe, não disparamos
            wantsShootThisFrame = false;
            return;
        }

        // alinhar corpo para o alvo (só no plano XZ)
        Vector3 flatDir = toTarget;
        flatDir.y = 0;
        flatDir.Normalize();

        if (flatDir.sqrMagnitude > 0.001f)
            RotateBodyTowards(flatDir);

        // ângulo geral (FOV)
        float angleToTarget = Vector3.Angle(transform.forward, flatDir);
        bool isInFov = angleToTarget <= fovDegrees * 0.5f;
        if (!isInFov)
        {
            if (drawRays) Debug.DrawRay(transform.position, transform.forward * 2f, Color.yellow);
            wantsShootThisFrame = false;
            return;
        }

        // linha de visão
        if (!CheckLineOfSight(aimPoint))
        {
            if (debugLogs) Debug.Log("[BotCombat] Linha de visão bloqueada.");
            wantsShootThisFrame = false;
            return;
        }

        // precisa estar bem alinhado para disparar
        bool isWellAligned = angleToTarget <= maxShootAngle;
        if (!isWellAligned)
        {
            wantsShootThisFrame = false;
            return;
        }

        // cooldown pronto?
        if (Time.time < nextFireTime)
        {
            wantsShootThisFrame = false;
            return;
        }

        // Obter arma activa
        int currentWeaponIndex = weaponManager.GetCurrentWeaponIndex();
        Weapon activeWeaponScript = weaponManager.GetActiveWeaponScript();

        if (activeWeaponScript == null)
        {
            if (debugLogs) Debug.LogError("[BotCombat] Não foi possível obter o script da arma ativa!");
            wantsShootThisFrame = false;
            return;
        }

        // Agora decidimos comportamento por arma
        if (currentWeaponIndex == RIFLE_INDEX)
        {
            HandleRifleLogic(activeWeaponScript);
        }
        else if (currentWeaponIndex == PISTOL_INDEX)
        {
            HandlePistolLogic(activeWeaponScript);
        }
        else
        {
            HandleGenericWeaponLogic(activeWeaponScript);
        }
    }


    // ==========================
    // LÓGICA DE TIRO / RELOAD / TROCA
    // ==========================

    void HandleRifleLogic(Weapon wpn)
    {
        int ammoNoCarregador = wpn.GetCurrentAmmo();

        if (ammoNoCarregador > 0)
        {
            // Ainda há balas no carregador da AK → dispara AK
            wantsShootThisFrame = true;

            nextFireTime = Time.time + rifleFireCooldown;

            if (debugLogs) Debug.Log("[BotCombat] SHOOT (Rifle / AK)");
            return;
        }

        // Chegou aqui = carregador da AK chegou a 0
        // --> NÃO recarrega em combate
        // --> troca imediatamente para pistola

        wantsShootThisFrame = false; // não puxar gatilho este frame (estamos a trocar arma)

        if (debugLogs) Debug.Log("[BotCombat] AK sem balas no carregador → SACAR PISTOLA.");

        weaponManager.SelectWeapon(PISTOL_INDEX);

        // mete cooldown de troca de arma
        nextFireTime = Time.time + weaponManager.weaponSwitchCooldown;
    }


    void HandlePistolLogic(Weapon wpn)
    {
        int ammoNoCarregador = wpn.GetCurrentAmmo();
        int ammoReserva = wpn.GetReserveAmmo();

        if (ammoNoCarregador > 0)
        {
            // pistola ainda tem balas → dispara pistola
            wantsShootThisFrame = true;

            nextFireTime = Time.time + pistolFireCooldown;

            if (debugLogs) Debug.Log("[BotCombat] SHOOT (Pistola)");
            return;
        }

        // pistola está vazia no carregador

        if (ammoReserva > 0)
        {
            // pistola vazia mas ainda há reserve -> recarregar pistola
            wantsShootThisFrame = false;

            if (debugLogs) Debug.Log("[BotCombat] Pistola vazia → reload rápido.");
            wpn.TryReload();

            // pequeno cooldown para não spammar reload todos os frames
            nextFireTime = Time.time + 0.3f;
            return;
        }

        // pistola também seca total
        wantsShootThisFrame = false;
        if (debugLogs) Debug.Log("[BotCombat] Pistola sem munição nenhuma.");
        nextFireTime = Time.time + 1.0f;
    }


    void HandleGenericWeaponLogic(Weapon wpn)
    {
        // fallback se algum dia houver facas / SMG etc
        int ammoNoCarregador = wpn.GetCurrentAmmo();
        int ammoReserva = wpn.GetReserveAmmo();

        if (ammoNoCarregador > 0)
        {
            wantsShootThisFrame = true;
            nextFireTime = Time.time + pistolFireCooldown;

            if (debugLogs) Debug.Log("[BotCombat] SHOOT (Genérica)");
            return;
        }

        if (ammoReserva > 0)
        {
            wantsShootThisFrame = false;
            if (debugLogs) Debug.Log("[BotCombat] Genérica vazia → reload.");
            wpn.TryReload();
            nextFireTime = Time.time + 0.3f;
            return;
        }

        wantsShootThisFrame = false;
        if (debugLogs) Debug.Log("[BotCombat] Genérica sem munição total.");
        nextFireTime = Time.time + 1.0f;
    }

    // ==========================
    // SUPORTE
    // ==========================

    void ApplyShootIntentToWeapon(bool wantsShoot)
    {
        // Esta é a ponte para a Weapon nova.
        // Se true → arma vai tentar disparar no Update dela (com cooldown interno, munição, reload automático, etc)
        // Se false → ela não dispara.
        if (weaponScript != null)
        {
            weaponScript.SetAIWantsShoot(wantsShoot);
        }
    }

    void FindPlayerTarget()
    {
        // tenta por Tag
        if (!player && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform.root;
        }

        // fallback por layer
        if (!player && playerLayer.value != 0)
        {
            var allHealths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (var h in allHealths)
            {
                bool sameLayer = ((1 << h.gameObject.layer) & playerLayer.value) != 0;
                if (sameLayer && !h.isDead)
                {
                    player = h.transform.root;
                    break;
                }
            }
        }

        if (!player && debugLogs)
            Debug.LogWarning("[BotCombat] Player não encontrado!");
    }


    bool CheckLineOfSight(Vector3 targetPoint)
    {
        if (!eyes)
            return true; // fallback seguro

        Vector3 from = eyes.position;
        Vector3 dir = (targetPoint - from);
        float dist = dir.magnitude;
        Vector3 norm = dir.normalized;

        if (drawRays)
            Debug.DrawRay(from, norm * dist, Color.cyan);

        // Raycast para ver se há obstáculos entre bot e alvo
        if (Physics.Raycast(from, norm, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (drawRays)
                Debug.DrawRay(from, norm * dist, Color.red);
            return false;
        }

        if (drawRays)
            Debug.DrawRay(from, norm * dist, Color.green);
        return true;
    }


    void RotateBodyTowards(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(flatDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            aimTurnSpeed * Time.deltaTime
        );
    }
}
