using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI_Proto : NetworkBehaviour
{
    // Estados possíveis do bot
    public enum BotState
    {
        Patrol,     // Patrulhar
        Chase,      // Perseguir jogador
        Attack,     // Atacar jogador
        Search,     // Procurar jogador após o perder de vista
        Retreat,    // Fugir para recuperar saúde
        GoToAmmo    // Ir buscar munição
    }

    [Header("Debug")]
    [SerializeField] BotState currentState = BotState.Patrol;
    public bool debugLogs = false;

    [Header("Referências")]
    public Transform eyes;            // Ponto de origem para o raycast de visão
    public Animator animator;         // Animator para animações
    public BotCombat combat;          // Script de combate do bot
    public MonoBehaviour healthSource;
    public string healthCurrentField = "currentHealth";
    public string healthMaxField = "maxHealth";

    [Header("Patrulha")]
    public Transform[] patrolPoints;  // Pontos de patrulha
    public float waypointTolerance = 1.0f;

    [Header("Pickups")]
    public Transform[] healthPickups; // Localização de health pickups
    public Transform[] ammoPickups;   // Localização de ammo pickups

    [Header("Visão / Target")]
    public string playerTag = "Player";
    public LayerMask obstacleMask = ~0;   // Máscara de obstáculos
    public float viewRadius = 60f;        // Distância de visão
    public float maxSearchTime = 10f;     // Tempo máximo a procurar jogador após o perder

    [Header("Otimização (NOVO)")]
    [Tooltip("Intervalo em segundos entre verificações de Raycast de visão.")]
    public float visionCheckInterval = 0.2f; 

    [Header("Combate")]
    public float idealCombatDistance = 10f;   // Distância ideal para combate
    public float tooCloseDistance = 4f;       // Distância mínima para recuar
    public float giveUpDistance = 120f;       // Distância máxima para desistir de perseguir

    [Header("Prioridades")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.2f;
    [Range(0f, 1f)] public float lowAmmoThreshold = 0.2f;

    [Header("Fuga")]
    public float retreatSpeedMultiplier = 1.5f;

    [Header("Comunicação")]
    public static List<BotAI_Proto> allBots = new List<BotAI_Proto>();
    public float alertRadius = 25f; // Distância para alertar aliados próximos

    // Componentes internos
    NavMeshAgent agent;
    Transform currentTarget;
    bool isDead = false;

    float baseSpeed;
    int patrolIndex = -1;
    int patrolDirection = 1;
    Vector3 lastKnownPlayerPos;
    float timeSinceLastSeen;
    float targetSearchTimer = 0f;

    float visionTimer = 0f;
    bool cachedVisibility = false;

    void OnEnable() => allBots.Add(this);
    void OnDisable() => allBots.Remove(this);

    void Awake()
    {
        // Obter referências essenciais
        agent = GetComponent<NavMeshAgent>();
        if (agent) baseSpeed = agent.speed;
        if (!eyes) eyes = transform;
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!combat) combat = GetComponent<BotCombat>();

        // Se healthSource não estiver definido, tenta obter componente "Health"
        if (healthSource == null)
        {
            var h = GetComponent("Health");
            if (h != null) healthSource = (MonoBehaviour)h;
        }

        patrolDirection = Random.value < 0.5f ? 1 : -1;  // Direção aleatória para patrulha
        patrolIndex = -1;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;  // Apenas o servidor controla a IA
            return;
        }
        ChangeState(BotState.Patrol);
    }

    void Update()
    {
        if (!IsServer || !agent || !agent.isOnNavMesh) return;

        // Verificar se está morto
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

        // Atualizar timer de busca de jogador
        targetSearchTimer += Time.deltaTime;
        if (targetSearchTimer > 0.5f)
        {
            FindClosestPlayer();
            targetSearchTimer = 0f;
        }

        // Verificar saúde e munição
        float health01 = GetHealth01();
        bool lowHealth = health01 > 0f && health01 <= lowHealthThreshold;

        float ammo01 = 1f;
        bool lowAmmo = false;
        if (combat)
        {
            ammo01 = combat.AmmoNormalized;
            lowAmmo = ammo01 <= lowAmmoThreshold;
        }

        // Determinar distância ao jogador e se é visível
        float distToPlayer = Mathf.Infinity;
        if (currentTarget)
        {
            distToPlayer = Vector3.Distance(transform.position, currentTarget.position);
            visionTimer += Time.deltaTime;
            if (visionTimer >= visionCheckInterval)
            {
                cachedVisibility = CheckVisibilityPhysics(distToPlayer);
                visionTimer = 0f;
            }
        }
        else cachedVisibility = false;

        bool playerVisible = cachedVisibility;

        if (playerVisible)
        {
            lastKnownPlayerPos = currentTarget.position;
            timeSinceLastSeen = 0f;
        }
        else timeSinceLastSeen += Time.deltaTime;

        // Mudança de estado baseada em saúde, munição e visão do jogador
        if (lowHealth) ChangeState(BotState.Retreat);
        else if (lowAmmo) ChangeState(BotState.GoToAmmo);
        else
        {
            if (playerVisible && distToPlayer <= giveUpDistance)
            {
                if (distToPlayer <= idealCombatDistance * 1.1f)
                    ChangeState(BotState.Attack);
                else
                    ChangeState(BotState.Chase);
            }
            else
            {
                if (timeSinceLastSeen > 0f && timeSinceLastSeen <= maxSearchTime &&
                    (currentState == BotState.Chase || currentState == BotState.Attack))
                    ChangeState(BotState.Search);
                else if (timeSinceLastSeen > maxSearchTime &&
                         (currentState == BotState.Search || currentState == BotState.Chase || currentState == BotState.Attack))
                    ChangeState(BotState.Patrol);
            }
        }

        // Executar a lógica de cada estado
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

    void HandleDeath()
    {
        // Desativar animações e movimento ao morrer
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
        // Encontrar o jogador mais próximo
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

    // ==================== LÓGICA DE CADA ESTADO ====================

    void TickPatrol()
    {
        // Patrulha entre pontos
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

        if (combat) combat.SetInCombat(false);
    }

    void TickChase()
    {
        if (!currentTarget) { ChangeState(BotState.Search); return; }

        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(currentTarget.position);

        if (combat) combat.SetInCombat(true);
    }

    void TickAttack()
    {
        if (!currentTarget) { ChangeState(BotState.Search); return; }

        Vector3 toPlayer = currentTarget.position - transform.position;
        float dist = toPlayer.magnitude;

        // Ajusta posicionamento para manter distância de combate
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

        if (combat) combat.SetInCombat(true);
    }

    void TickSearch()
    {
        // Ir até à última posição conhecida do jogador
        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(lastKnownPlayerPos);
        if (combat) combat.SetInCombat(false);
    }

    void TickRetreat()
    {
        // Fugir para health pickup ou afastar-se do jogador
        agent.isStopped = false;
        agent.speed = baseSpeed * retreatSpeedMultiplier;

        Transform hp = GetClosestTransform(healthPickups, transform.position);
        if (hp != null) agent.SetDestination(hp.position);
        else if (currentTarget)
        {
            Vector3 away = (transform.position - currentTarget.position).normalized;
            agent.SetDestination(transform.position + away * 8f);
        }

        if (combat) combat.SetInCombat(true);
    }

    void TickGoToAmmo()
    {
        agent.isStopped = false;
        agent.speed = baseSpeed;

        Transform ammo = GetClosestTransform(ammoPickups, transform.position);
        if (ammo != null) agent.SetDestination(ammo.position);
        else ChangeState(BotState.Patrol);

        if (combat) combat.SetInCombat(false);
    }

    // ==================== FUNÇÕES AUXILIARES ====================

    bool CheckVisibilityPhysics(float distToPlayer)
    {
        // Verifica se o jogador está dentro da visão e não está obstruído
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

    void ChangeState(BotState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        if (currentState == BotState.Search && lastKnownPlayerPos != Vector3.zero)
            agent.SetDestination(lastKnownPlayerPos);

        if (combat)
        {
            bool inCombat = (currentState == BotState.Chase) || (currentState == BotState.Attack) || (currentState == BotState.Retreat);
            combat.SetInCombat(inCombat);
        }

        if (currentState == BotState.Chase || currentState == BotState.Attack)
            AlertNearbyBots();
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
        if (currentState == BotState.Patrol || currentState == BotState.Search)
            ChangeState(BotState.Chase);
    }

    void UpdateAnimator()
    {
        if (!animator || !agent) return;
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    public void HearSound(Vector3 pos, float loudness) { }
}
