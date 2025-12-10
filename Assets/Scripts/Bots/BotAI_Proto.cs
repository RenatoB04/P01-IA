using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI_Proto : NetworkBehaviour
{
    public enum BotState
    {
        Patrol,
        Chase,
        Attack,
        Search,
        Retreat,
        GoToAmmo
    }

    [Header("Debug")]
    [SerializeField] BotState currentState = BotState.Patrol;
    public bool debugLogs = false;

    [Header("Referências")]
    public Transform eyes;
    public Animator animator;
    public BotCombat combat;

    // Campos de leitura de vida (compatibilidade)
    public MonoBehaviour healthSource;
    public string healthCurrentField = "currentHealth";
    public string healthMaxField = "maxHealth";

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float waypointTolerance = 1.0f;

    [Header("Pickups")]
    public Transform[] healthPickups;
    public Transform[] ammoPickups;

    [Header("Visão / Target")]
    public string playerTag = "Player";
    public LayerMask obstacleMask = ~0;
    public float viewRadius = 60f;
    public float maxSearchTime = 10f;

    [Header("Combate")]
    public float idealCombatDistance = 10f;
    public float tooCloseDistance = 4f;
    public float giveUpDistance = 120f;

    [Header("Prioridades")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.2f;
    [Range(0f, 1f)] public float lowAmmoThreshold = 0.2f;

    [Header("Fuga")]
    public float retreatSpeedMultiplier = 1.5f;

    [Header("Comunicação")]
    public static List<BotAI_Proto> allBots = new List<BotAI_Proto>();
    public float alertRadius = 25f;

    // --- Interno ---
    NavMeshAgent agent;
    Transform currentTarget;
    bool isDead = false;

    float baseSpeed;
    int patrolIndex = -1;
    int patrolDirection = 1;
    Vector3 lastKnownPlayerPos;
    float timeSinceLastSeen = 999f;
    float targetSearchTimer = 0f;

    void OnEnable()
    {
        allBots.Add(this);
    }

    void OnDisable()
    {
        allBots.Remove(this);
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent) baseSpeed = agent.speed;
        if (!eyes) eyes = transform;
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!combat) combat = GetComponent<BotCombat>();

        if (healthSource == null)
        {
            var h = GetComponent("Health");
            if (h != null) healthSource = (MonoBehaviour)h;
        }

        patrolDirection = Random.value < 0.5f ? 1 : -1;
        patrolIndex = -1;
    }

    public override void OnNetworkSpawn()
    {
        // A IA corre apenas no servidor
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        currentState = BotState.Patrol;
    }

    void Update()
    {
        if (!IsServer || !agent || !agent.isOnNavMesh) return;

        // 0. Verificar Morte (Prioridade Absoluta)
        var health = GetComponent<Health>();
        if (health != null && health.isDead.Value)
        {
            if (!isDead)
            {
                isDead = true;
                HandleDeath();
            }
            return;
        }

        // 1. Atualizar Sensores (Visão e Alvos)
        UpdateSensors();

        // 2. RODAR ÁRVORE DE DECISÃO (Define o currentState)
        Think();

        // 3. Executar Estado (Apenas move/ataca, não decide trocar de estado)
        switch (currentState)
        {
            case BotState.Patrol: TickPatrol(); break;
            case BotState.Chase: TickChase(); break;
            case BotState.Attack: TickAttack(); break;
            case BotState.Search: TickSearch(); break;
            case BotState.Retreat: TickRetreat(); break;
            case BotState.GoToAmmo: TickGoToAmmo(); break;
        }

        UpdateAnimator();
    }

    // =========================================================
    //               ÁRVORE DE DECISÃO (CÉREBRO)
    // =========================================================
    void Think()
    {
        // Nó 1: Sobrevivência (Vida Baixa) -> FOGE
        if (GetHealth01() <= lowHealthThreshold)
        {
            currentState = BotState.Retreat;
            return;
        }

        // Nó 2: Recursos (Munição Baixa) -> VAI BUSCAR BALAS
        if (combat != null && combat.AmmoNormalized <= lowAmmoThreshold)
        {
            currentState = BotState.GoToAmmo;
            return;
        }

        // Nó 3: Combate (Visão Direta) -> ATACA ou PERSEGUE
        if (currentTarget != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, currentTarget.position);
            bool canSee = CanSeePlayer(distToPlayer);

            if (canSee)
            {
                // Se vejo o jogador:
                // Perto o suficiente? Ataca.
                // Longe? Persegue.
                if (distToPlayer <= idealCombatDistance * 1.1f)
                    currentState = BotState.Attack;
                else
                    currentState = BotState.Chase;

                return; // Decisão tomada
            }
        }

        // Nó 4: Memória (Perdi de vista recentemente) -> PROCURA
        if (timeSinceLastSeen < maxSearchTime)
        {
            currentState = BotState.Search;
            return;
        }

        // Nó 5: Default -> PATRULHA
        currentState = BotState.Patrol;
    }

    // =========================================================
    //               SENSORES E INTERNOS
    // =========================================================

    void UpdateSensors()
    {
        // Procurar jogador periodicamente
        targetSearchTimer += Time.deltaTime;
        if (targetSearchTimer > 0.5f)
        {
            FindClosestPlayer();
            targetSearchTimer = 0f;
        }

        // Atualizar memória de visão
        if (currentTarget)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (CanSeePlayer(dist))
            {
                lastKnownPlayerPos = currentTarget.position;
                timeSinceLastSeen = 0f;
                AlertNearbyBots(); // Avisar aliados se vi o jogador
            }
            else
            {
                timeSinceLastSeen += Time.deltaTime;
            }
        }
    }

    void HandleDeath()
    {
        if (animator)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("Speed", 0f);
        }

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (combat) combat.SetInCombat(false);
    }

    void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform closest = null;
        float minDst = Mathf.Infinity;
        Vector3 myPos = transform.position;

        foreach (var p in players)
        {
            var h = p.GetComponent<Health>();
            if (h != null && h.isDead.Value) continue;

            float d = Vector3.Distance(p.transform.position, myPos);
            if (d < minDst)
            {
                minDst = d;
                closest = p.transform;
            }
        }

        currentTarget = closest;
        if (combat) combat.SetTarget(currentTarget);
    }

    // --- EXECUÇÃO DOS ESTADOS (SEM LOGICA DE TROCA, SÓ AÇÃO) ---

    void TickPatrol()
    {
        if (combat) combat.SetInCombat(false);

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.speed = baseSpeed;

        if (patrolIndex < 0 || patrolIndex >= patrolPoints.Length)
        {
            patrolIndex = Random.Range(0, patrolPoints.Length);
            agent.SetDestination(patrolPoints[patrolIndex].position);
            return;
        }

        Transform curWp = patrolPoints[patrolIndex];
        if (!curWp) { AdvancePatrolIndex(); return; }

        float sqrDist = (curWp.position - transform.position).sqrMagnitude;
        if (!agent.hasPath || sqrDist <= waypointTolerance * waypointTolerance)
        {
            AdvancePatrolIndex();
            if (patrolPoints[patrolIndex]) agent.SetDestination(patrolPoints[patrolIndex].position);
        }
    }

    void TickChase()
    {
        if (combat) combat.SetInCombat(true);
        if (!currentTarget) return;

        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(currentTarget.position);
    }

    void TickAttack()
    {
        if (combat) combat.SetInCombat(true);
        if (!currentTarget) return;

        Vector3 toPlayer = currentTarget.position - transform.position;
        float dist = toPlayer.magnitude;

        // Comportamento de combate simples
        if (dist > idealCombatDistance + 1f)
        {
            agent.isStopped = false;
            agent.speed = baseSpeed;
            agent.SetDestination(currentTarget.position);
        }
        else if (dist < tooCloseDistance)
        {
            agent.isStopped = false;
            Vector3 away = (transform.position - currentTarget.position).normalized;
            agent.SetDestination(transform.position + away * 3f);
        }
        else
        {
            agent.isStopped = true;
            agent.ResetPath();
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
        }
    }

    void TickSearch()
    {
        if (combat) combat.SetInCombat(false);

        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(lastKnownPlayerPos);
    }

    void TickRetreat()
    {
        if (combat) combat.SetInCombat(true); // Dispara enquanto foge

        agent.isStopped = false;
        agent.speed = baseSpeed * retreatSpeedMultiplier;

        Transform hp = GetClosestTransform(healthPickups, transform.position);
        if (hp != null)
        {
            agent.SetDestination(hp.position);
        }
        else if (currentTarget)
        {
            // Foge do jogador se não houver pickups
            Vector3 away = (transform.position - currentTarget.position).normalized;
            agent.SetDestination(transform.position + away * 8f);
        }
    }

    void TickGoToAmmo()
    {
        if (combat) combat.SetInCombat(false);

        agent.isStopped = false;
        agent.speed = baseSpeed;

        Transform ammo = GetClosestTransform(ammoPickups, transform.position);
        if (ammo != null)
            agent.SetDestination(ammo.position);
    }

    // --- UTILITÁRIOS ---

    bool CanSeePlayer(float distToPlayer)
    {
        if (!currentTarget) return false;
        if (distToPlayer > viewRadius) return false;

        Vector3 origin = eyes.position;
        Vector3 targetPos = currentTarget.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - origin);
        float dist = dir.magnitude;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform != currentTarget && hit.collider.transform.root != currentTarget)
                return false;
        }
        return true;
    }

    void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (patrolDirection >= 0)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        else
        {
            patrolIndex--;
            if (patrolIndex < 0) patrolIndex = patrolPoints.Length - 1;
        }
    }

    Transform GetClosestTransform(Transform[] list, Vector3 from)
    {
        if (list == null || list.Length == 0) return null;
        Transform best = null;
        float bestSqr = float.MaxValue;
        foreach (var t in list)
        {
            if (!t) continue;
            float d = (t.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = t; }
        }
        return best;
    }

    float GetHealth01()
    {
        var h = GetComponent<Health>();
        if (h != null) return h.currentHealth.Value / h.maxHealth;
        return 1f;
    }

    void AlertNearbyBots()
    {
        if (!currentTarget) return;
        foreach (var bot in allBots)
        {
            if (!bot || bot == this) continue;
            float d = Vector3.Distance(transform.position, bot.transform.position);
            if (d <= alertRadius)
                bot.OnAllySpottedPlayer(currentTarget.position);
        }
    }

    public void OnAllySpottedPlayer(Vector3 pos)
    {
        lastKnownPlayerPos = pos;
        timeSinceLastSeen = 0f;
        // Nota: A árvore de decisão vai apanhar isto no "Think" e mudar para Search ou Chase automaticamente
    }

    void UpdateAnimator()
    {
        if (!animator || !agent) return;
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    public void HearSound(Vector3 pos, float loudness) { }
}