using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic; // Necessário para a Lista

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI_Proto : MonoBehaviour
{
    // === ESTADOS DA IA ===
    enum BotState { Patrol, Chase, Search, Attack, Retreat }

    [Header("Debug")]
    [SerializeField]
    private BotState currentState = BotState.Patrol; // Estado inicial

    [Header("Patrulha")]
    public Transform[] patrolPoints; // PREENCHIDO PELO SPAWNER
    [Tooltip("Quão perto o bot precisa chegar do waypoint para considerá-lo alcançado.")]
    public float waypointTolerance = 1.5f;

    [Header("Visão")]
    public float viewRadius = 15f;
    [Range(0, 180)] public float viewAngle = 110f;
    public LayerMask targetMask;     // Layer(s) dos alvos (ex: Player)
    public LayerMask obstacleMask;   // Layer(s) que bloqueiam visão (ex: Wall, Default)
    [Tooltip("Tempo (s) que o bot continua a perseguir após perder visão (antes de ir para 'Search').")]
    public float loseSightChaseTime = 5f;
    [Tooltip("Tempo (s) que o bot espera no último local conhecido, procurando.")]
    public float searchWaitTime = 5.0f;

    [Header("Combate e Fuga")]
    [Tooltip("Distância para parar de perseguir e começar a atacar.")]
    public float attackDistance = 8f;
    [Tooltip("Distância para parar de atacar e voltar a perseguir.")]
    public float chaseDistance = 10f;
    [Tooltip("Percentagem de vida (0.0 a 1.0) para começar a fugir.")]
    [Range(0f, 1f)] public float healthThreshold = 0.3f;
    [Tooltip("Multiplicador de velocidade ao fugir.")]
    public float retreatSpeedMultiplier = 1.5f;
    [Tooltip("Tempo (s) que o bot espera no ponto de fuga antes de reavaliar.")]
    public float retreatReassessTime = 2.0f;

    [Header("Animação")]
    public Animator animator;
    [Tooltip("Ajuste para sincronizar velocidade da animação com a do NavMeshAgent.")]
    public float animationSpeedMultiplier = 1.0f;

    // --- Componentes e Estado Interno ---
    private NavMeshAgent agent;
    private Health health;
    private Transform target;           // Alvo atual
    private int patrolIndex = -1;       // Índice do waypoint atual (-1 para forçar a busca inicial)
    private float loseTargetTimer;      // Temporizador para perder o alvo
    private float searchTimer;          // Temporizador para o estado Search
    private float retreatTimer;         // Temporizador para reavaliar no estado Retreat
    private Vector3 lastKnownTargetPosition; // Última posição onde o alvo foi visto
    private float originalSpeed;        // Velocidade normal de movimento
    private bool lostSightMessageShown = false; // Flag para debug de perda de visão

    // --- VARIÁVEL NOVA PARA A NOSSA "BOLA DE DETEÇÃO" (TASK 2) ---
    private List<Transform> targetsInTriggerRadius = new List<Transform>();
    // --- FIM DA VARIÁVEL NOVA ---

    // --- Inicialização ---
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;

        if (agent) originalSpeed = agent.speed;
        agent.stoppingDistance = 0.0f;
    }

    void OnEnable()
    {
        // Subscreve aos eventos de vida
        if (health)
        {
            health.OnDied.RemoveListener(OnDeath); health.OnDied.AddListener(OnDeath);
            health.OnTookDamage -= HandleTookDamage; health.OnTookDamage += HandleTookDamage;
        }

        // --- CORREÇÃO PATRULHA (TASK 5) ---
        target = null;
        patrolIndex = -1;
        currentState = BotState.Patrol;
        if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = originalSpeed;

            // --- LINHA MODIFICADA (TASK 5) ---
            // A patrulha é agora iniciada no DoPatrol()
            // SetNextPatrolPoint(); // <-- Comentado para evitar bug de "corrida"
        }
        else if (agent && !agent.isOnNavMesh)
        {
            Debug.LogError(gameObject.name + " está a tentar ativar fora do NavMesh!", this);
        }
        // --- FIM CORREÇÃO ---
    }

    void OnDisable()
    {
        // Limpa subscrições de eventos
        if (health)
        {
            health.OnDied.RemoveListener(OnDeath);
            health.OnTookDamage -= HandleTookDamage;
        }

        if (agent && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            if (agent.isOnNavMesh) agent.ResetPath();
        }
    }

    // --- Ciclo Principal ---
    void Update()
    {
        if (health == null || health.isDead || !agent || !agent.isOnNavMesh)
        {
            if (agent && agent.isActiveAndEnabled) agent.isStopped = true;
            return;
        }

        // A deteção de alvos acontece (Task 2)
        if (currentState != BotState.Retreat)
        {
            DetectTarget();
        }

        UpdateStateMachine(); // Corre a lógica do estado atual
        UpdateAnimation();    // Atualiza a animação
    }

    // --- Máquina de Estados ---
    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case BotState.Patrol: DoPatrol(); break;
            case BotState.Chase: DoChase(); break;
            case BotState.Search: DoSearch(); break;
            case BotState.Attack: DoAttack(); break;
            case BotState.Retreat: DoRetreat(); break;
        }
    }

    // --- Lógica de Cada Estado ---

    private void DoPatrol()
    {
        agent.isStopped = false;
        agent.speed = originalSpeed;

        // --- CORREÇÃO PATRULHA (TASK 5) ---
        // Se não tivermos um caminho (porque acabámos de nascer) OU se já chegámos ao destino
        // O PONTO E VÍRGULA (;) FOI REMOVIDO DA LINHA SEGUINTE
        if (!agent.hasPath || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + waypointTolerance))
        {
            SetNextPatrolPoint(); // Define o próximo ponto
        }
        // --- FIM CORREÇÃO ---
    }


    private void DoChase()
    {
        agent.isStopped = false;
        agent.speed = originalSpeed;

        if (target && HasLineOfSight(target))
        {
            lastKnownTargetPosition = target.position;
            loseTargetTimer = loseSightChaseTime;
            lostSightMessageShown = false;
        }
        else
        {
            // Perdeu linha de visão
            if (!lostSightMessageShown)
            {
                lostSightMessageShown = true;
            }

            loseTargetTimer -= Time.deltaTime;
            if (loseTargetTimer <= 0)
            {
                TransitionToSearch();
                return;
            }
        }

        if (agent.destination != lastKnownTargetPosition)
        {
            agent.SetDestination(lastKnownTargetPosition);
        }

        float distanceSqr = (transform.position - lastKnownTargetPosition).sqrMagnitude;
        if (distanceSqr <= attackDistance * attackDistance)
        {
            TransitionToAttack();
        }
    }

    private void DoSearch()
    {
        agent.isStopped = false;
        agent.speed = originalSpeed * 0.75f;

        if (agent.destination != lastKnownTargetPosition && (!agent.hasPath || agent.remainingDistance > waypointTolerance))
        {
            agent.SetDestination(lastKnownTargetPosition);
        }

        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            agent.isStopped = true;

            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0)
            {
                TransitionToPatrol();
            }
        }
    }

    private void DoAttack()
    {
        agent.isStopped = true;

        if (target == null)
        {
            TransitionToSearch();
            return;
        }

        // Vira-se rapidamente para o alvo (apenas no eixo Y)
        Vector3 direction = (target.position - transform.position);
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * agent.angularSpeed * 1.5f);
        }

        float distanceSqr = (transform.position - target.position).sqrMagnitude;
        if (distanceSqr > chaseDistance * chaseDistance)
        {
            TransitionToChase();
        }
        else if (!HasLineOfSight(target))
        {
            TransitionToSearch();
        }
    }

    private void DoRetreat()
    {
        agent.isStopped = false;

        if (!agent.pathPending && agent.remainingDistance < 1.0f)
        {
            agent.isStopped = true;
            retreatTimer -= Time.deltaTime;

            if (retreatTimer <= 0)
            {
                bool stillLowHealth = health.currentHealth / health.maxHealth <= healthThreshold;
                bool stillSeeTarget = target && HasLineOfSight(target);

                if (stillLowHealth && stillSeeTarget)
                {
                    FindAndSetFleePoint();
                }
                else
                {
                    TransitionToPatrol();
                }
            }
        }
    }

    // --- Métodos de Transição de Estado ---

    private void TransitionToPatrol()
    {
        currentState = BotState.Patrol;
        agent.speed = originalSpeed;
        agent.isStopped = false;
        target = null;
        lostSightMessageShown = false;

        // --- CORREÇÃO PATRULHA (TASK 5) ---
        // A chamada é feita aqui para garantir que o bot recomeça a patrulha
        // assim que entra neste estado
        SetNextPatrolPoint();
        // --- FIM CORREÇÃO ---
    }

    private void TransitionToChase()
    {
        currentState = BotState.Chase;
        agent.speed = originalSpeed;
        agent.isStopped = false;
        loseTargetTimer = loseSightChaseTime;
        lostSightMessageShown = false;
    }

    private void TransitionToSearch()
    {
        if (currentState == BotState.Search) return;
        currentState = BotState.Search;
        agent.speed = originalSpeed * 0.75f;
        agent.isStopped = false;
        searchTimer = searchWaitTime;
        lostSightMessageShown = false;
    }

    private void TransitionToAttack()
    {
        if (currentState == BotState.Attack) return;
        currentState = BotState.Attack;
        agent.isStopped = true;
        lostSightMessageShown = false;
    }

    private void TransitionToRetreat()
    {
        if (currentState == BotState.Retreat) return;

        if (target == null && lastKnownTargetPosition == Vector3.zero)
        {
            TransitionToPatrol();
            return;
        }

        currentState = BotState.Retreat;
        agent.speed = originalSpeed * retreatSpeedMultiplier;
        agent.isStopped = false;
        lostSightMessageShown = false;

        FindAndSetFleePoint();
    }

    // --- Lógica de Eventos e Funções Auxiliares ---

    private void HandleTookDamage(float damageAmount, Transform attacker)
    {
        if (health.isDead) return;

        target = attacker;
        lastKnownTargetPosition = attacker ? attacker.position : transform.position;

        float healthRatio = health.currentHealth / health.maxHealth;

        if (healthRatio <= healthThreshold && currentState != BotState.Retreat)
        {
            TransitionToRetreat();
        }
        else if (currentState != BotState.Retreat)
        {
            TransitionToChase();
        }
    }

    private void OnDeath()
    {
        if (agent && agent.isActiveAndEnabled) agent.enabled = false;
        if (animator) animator.SetFloat("Speed", 0f);
        currentState = BotState.Patrol;
        target = null;
    }

    private void SetNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;

        if (patrolPoints[patrolIndex] == null)
        {
            Debug.LogError(gameObject.name + " Patrol point at index " + patrolIndex + " is null!", this);
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        if (agent && agent.isOnNavMesh)
        {
            agent.SetDestination(patrolPoints[patrolIndex].position);
            agent.isStopped = false;
        }
    }

    private void FindAndSetFleePoint()
    {
        Vector3 fleeFromPos = (target != null) ? target.position : lastKnownTargetPosition;
        if (fleeFromPos == Vector3.zero) fleeFromPos = transform.position + transform.forward * -10f;

        Vector3 runDirection = transform.position - fleeFromPos;
        runDirection.y = 0;
        Vector3 runPoint = transform.position + runDirection.normalized * (viewRadius * 1.2f);

        if (NavMesh.SamplePosition(runPoint, out NavMeshHit hit, viewRadius * 1.5f, NavMesh.AllAreas))
        {
            if (agent.isOnNavMesh) agent.SetDestination(hit.position);
            retreatTimer = retreatReassessTime;
            agent.isStopped = false;
        }
        else
        {
            TransitionToPatrol();
        }
    }

    // --- FUNÇÃO DETECTTARGET (TASK 2) OTIMIZADA ---
    private void DetectTarget()
    {
        // Otimização: Não procura novos alvos se estiver a atacar ou a fugir
        if (currentState == BotState.Attack || currentState == BotState.Retreat) return;

        Transform closestVisibleTarget = null;
        float minDistanceSqr = viewRadius * viewRadius + 1f; // Começa com uma distância maior que o raio

        // --- NOVO LOOP: Usa a nossa lista da Trigger (targetsInTriggerRadius) ---
        // Iterar de trás para a frente para podermos remover nulos/mortos da lista
        for (int i = targetsInTriggerRadius.Count - 1; i >= 0; i--)
        {
            Transform potentialTarget = targetsInTriggerRadius[i];

            // --- Verificação de Segurança ---
            if (potentialTarget == null)
            {
                targetsInTriggerRadius.RemoveAt(i);
                continue; // Pula para o próximo
            }

            // Verifica se o alvo tem componente Health e está vivo
            Health targetHealth = potentialTarget.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.isDead)
            {
                targetsInTriggerRadius.RemoveAt(i);
                continue; // Ignora alvos inválidos ou mortos
            }

            Vector3 directionToTarget = potentialTarget.position - transform.position;
            float distanceSqr = directionToTarget.sqrMagnitude;

            if (distanceSqr > minDistanceSqr) continue;

            // Verifica Ângulo de Visão
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget.normalized);
            if (angleToTarget <= viewAngle * 0.5f)
            {
                // Verifica Linha de Visão (Raycast)
                if (HasLineOfSight(potentialTarget))
                {
                    minDistanceSqr = distanceSqr;
                    closestVisibleTarget = potentialTarget;
                }
            }
        } // --- FIM DO NOVO LOOP ---


        // --- Lógica de Transição Baseada na Deteção ---
        if (closestVisibleTarget != null)
        {
            if (target != closestVisibleTarget)
            {
                target = closestVisibleTarget;
                TransitionToChase();
            }
            else if (currentState == BotState.Search)
            {
                TransitionToChase();
            }
        }
    }


    private bool HasLineOfSight(Transform t)
    {
        if (t == null) return false;

        Vector3 eyesPosition = transform.position + Vector3.up * 1.6f + transform.forward * 0.3f;
        Vector3 targetCenter = t.position + Vector3.up * 1.0f;
        Collider targetCollider = t.GetComponentInChildren<Collider>();
        if (targetCollider) targetCenter = targetCollider.bounds.center;

        Vector3 direction = targetCenter - eyesPosition;
        float distance = direction.magnitude;

        if (Physics.Raycast(eyesPosition, direction.normalized, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.root != t.root)
            {
                return false;
            }
        }

        return true; // Visão limpa
    }

    private void UpdateAnimation()
    {
        if (!animator || !agent || !agent.isActiveAndEnabled)
        {
            if (animator) animator.SetFloat("Speed", 0f);
            return;
        }

        Vector3 desiredVelocity = agent.desiredVelocity;
        Vector3 localDesiredVel = transform.InverseTransformDirection(desiredVelocity);

        float speed = localDesiredVel.z / agent.speed;

        speed = Mathf.Clamp01(speed);

        animator.SetFloat("Speed", speed * animationSpeedMultiplier, 0.1f, Time.deltaTime);
    }

    // --- NOVAS FUNÇÕES PARA A "BOLA DE DETEÇÃO" (TRIGGER) (TASK 2) ---

    private void OnTriggerEnter(Collider other)
    {
        // Verifica se o que entrou está na "targetMask" (ex: a layer "Player")
        // (1 << other.gameObject.layer) é um truque para comparar a layer do objeto com a nossa mask
        if ((targetMask.value & (1 << other.gameObject.layer)) > 0)
        {
            // Verifica se tem "Health" e não está morto
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null && !targetHealth.isDead)
            {
                // Adiciona à lista, se ainda não estiver lá
                Transform targetRoot = other.transform.root; // Pega no "pai" principal do alvo
                if (!targetsInTriggerRadius.Contains(targetRoot))
                {
                    // Debug.Log(gameObject.name + " Target ENTROU na trigger: " + targetRoot.name);
                    targetsInTriggerRadius.Add(targetRoot);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Verifica se o que saiu está na "targetMask"
        if ((targetMask.value & (1 << other.gameObject.layer)) > 0)
        {
            Transform targetRoot = other.transform.root;
            // Remove da lista se estiver lá
            if (targetsInTriggerRadius.Contains(targetRoot))
            {
                // Debug.Log(gameObject.name + " Target SAIU da trigger: " + targetRoot.name);
                targetsInTriggerRadius.Remove(targetRoot);
            }
        }
    }

    public bool IsInCombatState()
    {
        // Estamos "em combate" se estivermos a Perseguir, Atacar, ou Procurar
        return currentState == BotState.Chase ||
               currentState == BotState.Attack ||
               currentState == BotState.Search;
    }

} // <-- ESTA É A ÚLTIMA CHAVE