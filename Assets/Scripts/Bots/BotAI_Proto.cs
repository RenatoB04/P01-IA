using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// IA offline dos bots: patrulha por waypoints, vê o player, persegue, ataca,
/// foge com pouca vida, pode ir a pickups de vida/ammo e comunica com outros bots.
/// NÃO usa Netcode (MonoBehaviour normal).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BotAI_Proto : MonoBehaviour
{
    public enum BotState
    {
        Patrol,
        Chase,
        Attack,
        Search,
        Retreat,   // fuga / ir buscar vida
        GoToAmmo   // ir buscar munições
    }

    [Header("Debug")]
    [SerializeField] BotState currentState = BotState.Patrol;
    public bool debugLogs = false;

    [Header("Refs")]
    public Transform eyes;                  // ponto de visão; se null usa transform
    public Animator animator;               // opcional, só para "Speed"
    public MonoBehaviour healthSource;      // script de vida (para ler vida normalizada)
    public string healthCurrentField = "currentHealth";
    public string healthMaxField = "maxHealth";
    public BotCombat combat;                // script de combate/arma

    [Header("Patrulha")]
    public Transform[] patrolPoints;        // todos os WPs possíveis (inclui caminhos normais)
    public float waypointTolerance = 1.0f;
    public float patrolRepathInterval = 0.25f;

    [Header("Pickups (Waypoints especiais)")]
    public Transform[] healthPickups;       // WPs onde há vida
    public Transform[] ammoPickups;         // WPs onde há munições

    [Header("Visão / Target")]
    public string playerTag = "Player";
    public LayerMask obstacleMask = ~0;     // paredes/chão etc (NÃO incluir o player)
    public float viewRadius = 80f;          // grande, para quase "mapa todo"
    public float maxSearchTime = 10f;       // tempo a procurar depois de perder visão

    [Header("Combate / Distâncias")]
    public float idealCombatDistance = 10f; // distância onde ele tenta ficar
    public float tooCloseDistance = 4f;     // se mais perto que isto, afasta-se
    public float giveUpDistance = 120f;     // se o player está mais longe que isto, ignora

    [Header("Prioridades")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.2f; // <20%
    [Range(0f, 1f)] public float lowAmmoThreshold = 0.2f;   // <20% (lido do BotCombat)

    [Header("Fuga")]
    public float retreatSpeedMultiplier = 1.5f;

    [Header("Comunicação entre bots")]
    public static List<BotAI_Proto> allBots = new List<BotAI_Proto>();
    public float alertRadius = 25f;         // raio onde avisa outros bots

    // --- Interno ---
    NavMeshAgent agent;
    Transform player;

    float baseSpeed;
    int patrolIndex = -1;
    int patrolDirection = 1;                // 1 = para a frente, -1 = para trás
    float patrolRepathTimer;

    Vector3 lastKnownPlayerPos;
    float timeSinceLastSeen;

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
            // tenta auto-encontrar um componente chamado "Health"
            var h = GetComponent("Health");
            if (h != null) healthSource = (MonoBehaviour)h;
        }

        // Escolher direcção aleatória de patrulha (subir ou descer o array de WPs)
        patrolDirection = Random.value < 0.5f ? 1 : -1;

        // patrolIndex fica a -1, a TickPatrol escolhe o primeiro destino
        patrolIndex = -1;
    }

    void Start()
    {
        FindPlayerRef();
        ChangeState(BotState.Patrol);
    }

    void FindPlayerRef()
    {
        if (!player && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }
    }

    void Update()
    {
        if (!agent || !agent.isOnNavMesh)
            return;

        FindPlayerRef();

        float health01 = GetHealth01();
        bool lowHealth = health01 > 0f && health01 <= lowHealthThreshold;

        float ammo01 = 1f;
        bool lowAmmo = false;
        if (combat)
        {
            ammo01 = combat.AmmoNormalized;
            lowAmmo = ammo01 <= lowAmmoThreshold;
        }

        bool playerVisible = false;
        float distToPlayer = Mathf.Infinity;
        if (player)
        {
            distToPlayer = Vector3.Distance(transform.position, player.position);
            playerVisible = CanSeePlayer(distToPlayer);
        }

        if (playerVisible)
        {
            lastKnownPlayerPos = player.position;
            timeSinceLastSeen = 0f;
        }
        else
        {
            timeSinceLastSeen += Time.deltaTime;
        }

        // --------- DECISÃO DE ESTADO (prioridades) ----------
        // 1) Vida baixa -> fugir / ir buscar vida
        if (lowHealth)
        {
            if (currentState != BotState.Retreat)
                ChangeState(BotState.Retreat);
        }
        // 2) Ammo baixa (e não lowHealth) -> ir buscar ammo
        else if (lowAmmo && currentState != BotState.GoToAmmo)
        {
            ChangeState(BotState.GoToAmmo);
        }
        else
        {
            // 3) Combate baseado na visão do player
            if (playerVisible && distToPlayer <= giveUpDistance)
            {
                if (distToPlayer <= idealCombatDistance * 1.1f)
                    ChangeState(BotState.Attack);
                else
                    ChangeState(BotState.Chase);
            }
            else
            {
                // perdeu visão: se ainda está dentro do tempo de procura
                if (timeSinceLastSeen > 0f && timeSinceLastSeen <= maxSearchTime &&
                    (currentState == BotState.Chase || currentState == BotState.Attack))
                {
                    ChangeState(BotState.Search);
                }
                else if (timeSinceLastSeen > maxSearchTime &&
                         (currentState == BotState.Search || currentState == BotState.Chase || currentState == BotState.Attack))
                {
                    ChangeState(BotState.Patrol);
                }
            }
        }

        // --------- EXECUÇÃO DO ESTADO ----------
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

    // --------------------------------------------------------------------
    // ESTADOS
    // --------------------------------------------------------------------
    void TickPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.speed = baseSpeed;

        // Se ainda não temos índice, escolhe um ponto inicial aleatório
        if (patrolIndex < 0 || patrolIndex >= patrolPoints.Length)
        {
            patrolIndex = Random.Range(0, patrolPoints.Length);
            agent.SetDestination(patrolPoints[patrolIndex].position);
            return;
        }

        Transform curWp = patrolPoints[patrolIndex];
        if (!curWp)
        {
            AdvancePatrolIndex();
            curWp = patrolPoints[patrolIndex];
            agent.SetDestination(curWp.position);
            return;
        }

        // Distância real ao waypoint (não confiar só em remainingDistance)
        float sqrDist = (curWp.position - transform.position).sqrMagnitude;
        float tol = waypointTolerance > 0f ? waypointTolerance : 0.6f;

        if (!agent.hasPath || sqrDist <= tol * tol)
        {
            // Passa ao próximo waypoint
            AdvancePatrolIndex();
            Transform nextWp = patrolPoints[patrolIndex];
            if (nextWp)
            {
                agent.SetDestination(nextWp.position);
            }
        }

        if (combat) combat.SetInCombat(false);
    }

    void TickChase()
    {
        if (!player)
        {
            ChangeState(BotState.Search);
            return;
        }

        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(player.position);

        if (combat) combat.SetInCombat(true);
    }

    void TickAttack()
    {
        if (!player)
        {
            ChangeState(BotState.Search);
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        // ajusta distância ideal
        if (dist > idealCombatDistance + 1f)
        {
            // aproximar
            agent.isStopped = false;
            agent.speed = baseSpeed;
            agent.SetDestination(player.position);
        }
        else if (dist < tooCloseDistance)
        {
            // recuar um pouco mas a combater
            agent.isStopped = false;
            Vector3 away = (transform.position - player.position).normalized;
            Vector3 dest = transform.position + away * 3f;
            agent.SetDestination(dest);
        }
        else
        {
            // zona confortável -> para de andar
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (combat) combat.SetInCombat(true);
    }

    void TickSearch()
    {
        // Vai até ao último local onde viu o player, fica lá um bocado,
        // depois volta a patrulhar (já é tratado no Update pelas transições tempo > maxSearchTime)
        agent.isStopped = false;
        agent.speed = baseSpeed;
        agent.SetDestination(lastKnownPlayerPos);

        if (combat) combat.SetInCombat(false);
    }

    void TickRetreat()
    {
        // tenta ir ao pickup de vida mais próximo; se não houver, foge na direcção oposta ao player
        agent.isStopped = false;
        agent.speed = baseSpeed * retreatSpeedMultiplier;

        Transform hp = GetClosestTransform(healthPickups, transform.position);
        if (hp != null)
        {
            agent.SetDestination(hp.position);
        }
        else if (player)
        {
            Vector3 away = (transform.position - player.position).normalized;
            Vector3 dest = transform.position + away * 8f;
            agent.SetDestination(dest);
        }

        // ainda pode disparar se o player estiver perto (decidido no BotCombat através de inCombat)
        if (combat) combat.SetInCombat(true);
    }

    void TickGoToAmmo()
    {
        agent.isStopped = false;
        agent.speed = baseSpeed;

        Transform ammo = GetClosestTransform(ammoPickups, transform.position);
        if (ammo != null)
        {
            agent.SetDestination(ammo.position);
        }
        else
        {
            // se não houver pontos de ammo definidos, volta a patrulhar
            ChangeState(BotState.Patrol);
        }

        if (combat) combat.SetInCombat(false);
    }

    // --------------------------------------------------------------------
    // VISÃO / DETEÇÃO
    // --------------------------------------------------------------------
    bool CanSeePlayer(float distToPlayer)
    {
        if (!player) return false;

        if (distToPlayer > viewRadius)
            return false;

        // verifica se não há paredes no meio (raycast dos olhos até ao player)
        Vector3 origin = eyes.position;
        Vector3 targetPos = player.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - origin);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // bateu em algo ANTES do player -> visão bloqueada
            if (hit.collider.transform != player && hit.collider.transform.root != player)
                return false;
        }

        return true;
    }

    // --------------------------------------------------------------------
    // UTILITÁRIOS
    // --------------------------------------------------------------------
    void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        if (patrolDirection >= 0)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
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
            if (d < bestSqr)
            {
                bestSqr = d;
                best = t;
            }
        }
        return best;
    }

    float GetHealth01()
    {
        if (healthSource == null) return 1f;

        var t = healthSource.GetType();

        float cur = 0f;
        float max = 0f;
        bool gotCur = false;
        bool gotMax = false;

        var fCur = t.GetField(healthCurrentField);
        if (fCur != null && (fCur.FieldType == typeof(float) || fCur.FieldType == typeof(int)))
        {
            cur = fCur.FieldType == typeof(int) ? (int)fCur.GetValue(healthSource) : (float)fCur.GetValue(healthSource);
            gotCur = true;
        }
        var pCur = t.GetProperty(healthCurrentField);
        if (!gotCur && pCur != null && (pCur.PropertyType == typeof(float) || pCur.PropertyType == typeof(int)))
        {
            cur = pCur.PropertyType == typeof(int) ? (int)pCur.GetValue(healthSource) : (float)pCur.GetValue(healthSource);
            gotCur = true;
        }

        var fMax = t.GetField(healthMaxField);
        if (fMax != null && (fMax.FieldType == typeof(float) || fMax.FieldType == typeof(int)))
        {
            max = fMax.FieldType == typeof(int) ? (int)fMax.GetValue(healthSource) : (float)fMax.GetValue(healthSource);
            gotMax = true;
        }
        var pMax = t.GetProperty(healthMaxField);
        if (!gotMax && pMax != null && (pMax.PropertyType == typeof(float) || pMax.PropertyType == typeof(int)))
        {
            max = pMax.PropertyType == typeof(int) ? (int)pMax.GetValue(healthSource) : (float)pMax.GetValue(healthSource);
            gotMax = true;
        }

        if (!gotCur || !gotMax || max <= 0.001f)
            return 1f;

        return Mathf.Clamp01(cur / max);
    }

    void ChangeState(BotState newState)
    {
        if (currentState == newState) return;

        if (debugLogs)
            Debug.Log($"[{name}] {currentState} -> {newState}");

        currentState = newState;

        if (currentState == BotState.Search)
        {
            // quando entra em Search, vai para último local visto
            if (lastKnownPlayerPos != Vector3.zero)
                agent.SetDestination(lastKnownPlayerPos);
        }

        // Comunicar com BotCombat
        if (combat)
        {
            bool inCombat =
                (currentState == BotState.Chase) ||
                (currentState == BotState.Attack) ||
                (currentState == BotState.Retreat); // pode disparar enquanto foge

            combat.SetInCombat(inCombat);
        }

        // Alertar outros bots quando entra em combate
        if (currentState == BotState.Chase || currentState == BotState.Attack)
        {
            AlertNearbyBots();
        }
    }

    void AlertNearbyBots()
    {
        if (!player) return;

        foreach (var bot in allBots)
        {
            if (!bot || bot == this) continue;
            float d = Vector3.Distance(transform.position, bot.transform.position);
            if (d <= alertRadius)
            {
                bot.OnAllySpottedPlayer(player.position);
            }
        }
    }

    public void OnAllySpottedPlayer(Vector3 pos)
    {
        lastKnownPlayerPos = pos;
        timeSinceLastSeen = 0f;

        if (currentState == BotState.Patrol || currentState == BotState.Search)
        {
            ChangeState(BotState.Chase);
        }
    }

    void UpdateAnimator()
    {
        if (!animator || !agent) return;

        Vector3 vel = agent.velocity;
        float speed = vel.magnitude;
        animator.SetFloat("Speed", speed);
    }

    // Chamado no futuro por sistema de som (explosões, tiros, etc.)
    public void HearSound(Vector3 pos, float loudness)
    {
        // --- APAGA OU COMENTA TUDO O QUE ESTÁ AQUI DENTRO ---

        /* float dist = Vector3.Distance(transform.position, pos);
        if (dist < viewRadius * 0.6f)
        {
            lastKnownPlayerPos = pos;
            timeSinceLastSeen = 0f;
            if (currentState == BotState.Patrol)
                ChangeState(BotState.Search);
        }
        */
    }
}
