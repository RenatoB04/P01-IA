using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic; // Necessário se usares Listas no futuro

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
    public float animationSpeedMultiplier = 1.0f; // Renomeado para clareza

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

    // --- Inicialização ---
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false; // Movimento controlado pelo NavMeshAgent

        if (agent) originalSpeed = agent.speed; // Guarda a velocidade original
        agent.stoppingDistance = 0.0f; // Controlamos a paragem manualmente nos estados
    }

    void OnEnable()
    {
        // Subscreve aos eventos de vida
        if (health)
        {
            health.OnDied.RemoveListener(OnDeath); health.OnDied.AddListener(OnDeath);
            health.OnTookDamage -= HandleTookDamage; health.OnTookDamage += HandleTookDamage;
        }

        // --- CORREÇÃO PATRULHA: Garante estado inicial limpo e define o primeiro waypoint ---
        target = null;
        patrolIndex = -1; // Força SetNextPatrolPoint a escolher o primeiro (índice 0)
        currentState = BotState.Patrol; // Define explicitamente o estado inicial
        if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false; // Garante que começa a mover-se
            agent.speed = originalSpeed; // Garante velocidade correta
            SetNextPatrolPoint(); // Define o primeiro destino
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
        // Para o agente se for desativado
        if (agent && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            if(agent.isOnNavMesh) agent.ResetPath(); // Só faz ResetPath se estiver no NavMesh
        }
    }

    // --- Ciclo Principal ---
    void Update()
    {
        // Condições para parar a IA (morto, sem vida, fora do NavMesh)
        if (health == null || health.isDead || !agent || !agent.isOnNavMesh)
        {
            // Garante que o agente para se algo correr mal
            if (agent && agent.isActiveAndEnabled) agent.isStopped = true;
            return;
        }

        // A deteção de alvos acontece independentemente do estado (exceto Retreat)
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
        // // Descomentar para ver o estado atual a cada frame:
        // Debug.Log(gameObject.name + " State: " + currentState + " | Target: " + (target ? target.name : "None") + " | Destination: " + agent.destination);

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
        // Garante que se está a mover
        agent.isStopped = false;
        agent.speed = originalSpeed; // Garante velocidade normal

        // --- CORREÇÃO PATRULHA: Verifica se tem caminho e se chegou ---
        // Se não tem caminho (talvez o ponto seja inválido) OU se chegou ao destino
        if ((!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + waypointTolerance) || !agent.hasPath)
        {
            // // Debug:
            // Debug.Log(gameObject.name + " Reached patrol point or has no path. Setting next...");
            SetNextPatrolPoint(); // Define o próximo ponto
        }
        // --- FIM CORREÇÃO ---
    }


    private void DoChase()
    {
        agent.isStopped = false; // Garante movimento
        agent.speed = originalSpeed; // Velocidade normal

        if (target && HasLineOfSight(target))
        {
            lastKnownTargetPosition = target.position;
            loseTargetTimer = loseSightChaseTime; // Reinicia timer
            lostSightMessageShown = false;
        }
        else
        {
            // Perdeu linha de visão
            if (!lostSightMessageShown)
            {
                // Debug.Log(gameObject.name + " LOST Line of Sight. Starting lose timer (" + loseSightChaseTime + "s)...");
                lostSightMessageShown = true;
            }

            loseTargetTimer -= Time.deltaTime;
            if (loseTargetTimer <= 0)
            {
                // Debug.Log(gameObject.name + " Lose timer expired. Transitioning to Search.");
                TransitionToSearch();
                return; // Sai da função após transição
            }
        }

        // Define o destino (otimizado para só definir se mudar)
        if (agent.destination != lastKnownTargetPosition)
        {
            agent.SetDestination(lastKnownTargetPosition);
        }

        // Verifica se está perto o suficiente para atacar
        float distanceSqr = (transform.position - lastKnownTargetPosition).sqrMagnitude; // Mais rápido que Vector3.Distance
        if (distanceSqr <= attackDistance * attackDistance)
        {
            TransitionToAttack();
        }
    }

    private void DoSearch()
    {
        agent.isStopped = false; // Garante que vai até ao local
        agent.speed = originalSpeed * 0.75f; // Anda um pouco mais devagar a procurar (opcional)

        // Define o destino se ainda não chegou
        if (agent.destination != lastKnownTargetPosition && (!agent.hasPath || agent.remainingDistance > waypointTolerance))
        {
             agent.SetDestination(lastKnownTargetPosition);
        }

        // Quando chega ao último local conhecido
        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            agent.isStopped = true; // Para de andar

            // Opcional: Animação de "procurar", rodar a cabeça, etc.

            searchTimer -= Time.deltaTime;
            // Debug.Log(gameObject.name + " Searching at last known position... Time left: " + searchTimer);
            if (searchTimer <= 0)
            {
                // Debug.Log(gameObject.name + " Search timer expired. Giving up and transitioning to Patrol.");
                TransitionToPatrol(); // Desiste e volta a patrulhar
            }
        }
    }

    private void DoAttack()
    {
        agent.isStopped = true; // Para de andar para mirar

        if (target == null) // Alvo desapareceu?
        {
            // Debug.Log(gameObject.name + " Target lost during Attack. Transitioning to Search.");
            TransitionToSearch(); // Procura na última posição
            return;
        }

        // Vira-se rapidamente para o alvo (apenas no eixo Y)
        Vector3 direction = (target.position - transform.position);
        direction.y = 0; // Ignora diferença de altura para a rotação
        if (direction.sqrMagnitude > 0.01f) // Evita LookRotation(0,0,0)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * agent.angularSpeed * 1.5f); // Roda mais rápido
        }


        // Verifica condições para sair do estado Attack
        float distanceSqr = (transform.position - target.position).sqrMagnitude;
        if (distanceSqr > chaseDistance * chaseDistance)
        {
            // Debug.Log(gameObject.name + " Target moved too far. Transitioning back to Chase.");
            TransitionToChase(); // Alvo afastou-se
        }
        else if (!HasLineOfSight(target))
        {
             // Debug.Log(gameObject.name + " Lost LOS during Attack but still close. Transitioning to Search.");
             TransitionToSearch(); // Perdeu visão, investiga
        }
        // Se continua perto e com visão, permanece em Attack (o script BotCombat trata do tiro)
    }

    private void DoRetreat()
    {
        agent.isStopped = false; // Garante que está a correr

        // Chegou ao ponto de fuga?
        if (!agent.pathPending && agent.remainingDistance < 1.0f) // Tolerância maior para fuga
        {
            agent.isStopped = true; // Para temporariamente
            retreatTimer -= Time.deltaTime;

            if (retreatTimer <= 0)
            {
                 // Reavalia a situação
                 bool stillLowHealth = health.currentHealth / health.maxHealth <= healthThreshold;
                 // Verifica se ainda VÊ o alvo (se ele existir)
                 bool stillSeeTarget = target && HasLineOfSight(target);

                 // // Debug:
                 // Debug.Log(gameObject.name + " Reassessing retreat. Low Health: " + stillLowHealth + ", Still See Target: " + stillSeeTarget);

                // Só continua a fugir se AINDA estiver com pouca vida E AINDA vir o alvo
                if (stillLowHealth && stillSeeTarget)
                {
                    // Debug.Log(gameObject.name + " Still in danger, finding new flee point.");
                    FindAndSetFleePoint(); // Encontra um novo ponto de fuga
                }
                else
                {
                    // Debug.Log(gameObject.name + " Danger passed (or target lost), transitioning to Patrol.");
                    TransitionToPatrol(); // Condições de fuga não se mantêm, volta a patrulhar
                }
            }
        }
        // Se ainda não chegou, continua a correr (isStopped = false já definido no início)
    }

    // --- Métodos de Transição de Estado ---
    // (Adicionado Debug.Log em cada transição para clareza)

    private void TransitionToPatrol()
    {
        // // Otimização: Evita redefinir se já está a patrulhar E a mover-se para um ponto
        // if (currentState == BotState.Patrol && agent.hasPath && !agent.isStopped) return;

        // Debug.Log(gameObject.name + " ---> Transitioning to PATROL");
        currentState = BotState.Patrol;
        agent.speed = originalSpeed;
        agent.isStopped = false;
        target = null; // Garante que limpa o alvo
        lostSightMessageShown = false;
        // --- CORREÇÃO PATRULHA: Define o próximo ponto ao entrar no estado ---
        SetNextPatrolPoint();
        // --- FIM CORREÇÃO ---
    }

    private void TransitionToChase()
    {
        // if (currentState == BotState.Chase) return; // Otimização pode ser removida se quisermos resetar o timer ao levar dano
        // Debug.Log(gameObject.name + " ---> Transitioning to CHASE");
        currentState = BotState.Chase;
        agent.speed = originalSpeed;
        agent.isStopped = false;
        loseTargetTimer = loseSightChaseTime; // Reinicia o temporizador de perda de visão
        lostSightMessageShown = false;
    }

    private void TransitionToSearch()
    {
        if (currentState == BotState.Search) return;
        // Debug.Log(gameObject.name + " ---> Transitioning to SEARCH");
        currentState = BotState.Search;
        agent.speed = originalSpeed * 0.75f; // Reduz a velocidade para procurar
        agent.isStopped = false;
        searchTimer = searchWaitTime; // Inicia o temporizador de procura
        lostSightMessageShown = false;
    }

    private void TransitionToAttack()
    {
        if (currentState == BotState.Attack) return;
        // Debug.Log(gameObject.name + " ---> Transitioning to ATTACK");
        currentState = BotState.Attack;
        agent.isStopped = true; // Para para poder mirar e disparar
        lostSightMessageShown = false;
    }

    private void TransitionToRetreat()
    {
         if (currentState == BotState.Retreat) return;
         // Debug.Log(gameObject.name + " ---> Transitioning to RETREAT");

        // Condição de segurança: só foge se souber de quem/onde
        if (target == null && lastKnownTargetPosition == Vector3.zero)
        {
            // Debug.LogWarning(gameObject.name + " Tried to retreat but has no target/last known position. Defaulting to Patrol.");
            TransitionToPatrol(); // Foge para Patrulha se não tiver informação
            return;
        }

        currentState = BotState.Retreat;
        agent.speed = originalSpeed * retreatSpeedMultiplier; // Aumenta a velocidade
        agent.isStopped = false;
        lostSightMessageShown = false;

        FindAndSetFleePoint(); // Calcula e define o ponto de fuga
    }

    // --- Lógica de Eventos e Funções Auxiliares ---

    private void HandleTookDamage(float damageAmount, Transform attacker)
    {
        if (health.isDead) return; // Ignora se já morreu

        // // Debug:
        // Debug.Log(gameObject.name + " Took " + damageAmount + " damage from " + (attacker ? attacker.name : "Unknown"));

        // Atualiza sempre quem atacou por último e onde estava
        target = attacker;
        lastKnownTargetPosition = attacker ? attacker.position : transform.position; // Se attacker for nulo, usa a posição atual como referência

        float healthRatio = health.currentHealth / health.maxHealth;

        // Condição para Fuga: Pouca vida E NÃO está já a fugir
        if (healthRatio <= healthThreshold && currentState != BotState.Retreat)
        {
            TransitionToRetreat();
        }
        // Condição para Perseguir: Levou dano e NÃO vai fugir
        // Muda para Chase mesmo que estivesse a atacar outro alvo (prioriza quem o atacou)
        else if (currentState != BotState.Retreat) // Só não interrompe a fuga
        {
             // Debug.Log(gameObject.name + " Took damage, ensuring Chase state for attacker.");
             TransitionToChase(); // Garante que persegue o novo atacante (ou reinicia o timer se já o perseguia)
        }
    }

    private void OnDeath()
    {
        // Debug.Log(gameObject.name + " Died.");
        if(agent && agent.isActiveAndEnabled) agent.enabled = false; // Desativa o agente
        if (animator) animator.SetFloat("Speed", 0f); // Para a animação
        currentState = BotState.Patrol; // Reseta estado para próximo respawn
        target = null;
    }

    // --- CORREÇÃO PATRULHA: Lógica melhorada para escolher o próximo ponto ---
    private void SetNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            // Debug.LogWarning(gameObject.name + " No patrol points assigned. Staying idle.");
            if (agent && agent.isOnNavMesh) agent.isStopped = true; // Para se não tiver para onde ir
            return;
        }

        // Escolhe o próximo índice de forma circular
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;

        // Verifica se o ponto escolhido é válido
        if (patrolPoints[patrolIndex] == null)
        {
             Debug.LogError(gameObject.name + " Patrol point at index " + patrolIndex + " is null!", this);
             // Tenta o próximo ponto (recursivo, mas cuidado com loop infinito se todos forem nulos)
             // SetNextPatrolPoint(); // Poderia causar problemas, melhor parar
             if (agent && agent.isOnNavMesh) agent.isStopped = true;
             return;
        }

        // Define o destino
        if (agent && agent.isOnNavMesh)
        {
            // Debug.Log(gameObject.name + " Setting patrol destination to: " + patrolPoints[patrolIndex].name + " at " + patrolPoints[patrolIndex].position);
            agent.SetDestination(patrolPoints[patrolIndex].position);
            agent.isStopped = false; // Garante que começa a mover-se
        }
    }
    // --- FIM CORREÇÃO ---


    private void FindAndSetFleePoint()
    {
        Vector3 fleeFromPos = (target != null) ? target.position : lastKnownTargetPosition;
        if (fleeFromPos == Vector3.zero) fleeFromPos = transform.position + transform.forward * -10f; // Fallback

        Vector3 runDirection = transform.position - fleeFromPos;
        runDirection.y = 0; // Foge no plano horizontal
        // Tenta encontrar um ponto um pouco mais longe
        Vector3 runPoint = transform.position + runDirection.normalized * (viewRadius * 1.2f);

        // // Debug Visual:
        // Debug.DrawRay(transform.position, runDirection.normalized * 10f, Color.red, 3f);

        // Procura um ponto válido no NavMesh na direção de fuga
        if (NavMesh.SamplePosition(runPoint, out NavMeshHit hit, viewRadius * 1.5f, NavMesh.AllAreas))
        {
             // // Debug:
             // Debug.Log(gameObject.name + " Found flee point. Setting destination to: " + hit.position);
             // Debug.DrawLine(transform.position, hit.position, Color.magenta, 3f);
             if(agent.isOnNavMesh) agent.SetDestination(hit.position);
             retreatTimer = retreatReassessTime; // Inicia temporizador de reavaliação
             agent.isStopped = false; // Garante que começa a correr
        }
        else
        {
            // // Debug:
            // Debug.LogWarning(gameObject.name + " Failed to find NavMesh point to flee to. Defaulting to Patrol.");
            TransitionToPatrol(); // Não encontrou sítio para fugir, desiste
        }
    }

    private void DetectTarget()
    {
        // Otimização: Não procura novos alvos se estiver a atacar ou a fugir
        if (currentState == BotState.Attack || currentState == BotState.Retreat) return;

        // Procura colliders na layer de alvos dentro do raio de visão
        Collider[] hitsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask, QueryTriggerInteraction.Ignore);
        Transform closestVisibleTarget = null;
        float minDistanceSqr = viewRadius * viewRadius + 1f; // Começa com uma distância maior que o raio

        foreach (var hitCollider in hitsInViewRadius)
        {
            // Ignora a si mesmo
            if (hitCollider.transform.root == transform.root) continue;

            // Verifica se o alvo tem componente Health e está vivo
             Health targetHealth = hitCollider.GetComponentInParent<Health>(); // Procura Health no objeto ou nos pais
             if (targetHealth == null || targetHealth.isDead) continue; // Ignora alvos inválidos ou mortos

            Transform potentialTarget = hitCollider.transform; // Ou .root se preferires
            Vector3 directionToTarget = potentialTarget.position - transform.position;
            float distanceSqr = directionToTarget.sqrMagnitude; // Mais rápido que Distance()

            // Otimização: Se já está mais longe que o alvo mais próximo encontrado, ignora
            if (distanceSqr > minDistanceSqr) continue;

            // Verifica Ângulo de Visão
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget.normalized);
            if (angleToTarget <= viewAngle * 0.5f)
            {
                // Verifica Linha de Visão (Raycast)
                if (HasLineOfSight(potentialTarget))
                {
                    // Este é o alvo visível mais próximo até agora
                    minDistanceSqr = distanceSqr;
                    closestVisibleTarget = potentialTarget;
                }
            }
        }

        // --- Lógica de Transição Baseada na Deteção ---
        if (closestVisibleTarget != null)
        {
            // Encontrou um alvo visível
            if (target != closestVisibleTarget) // Se for um alvo novo (ou se tinha perdido o anterior)
            {
                // Debug.Log(gameObject.name + " DETECTED Target: " + closestVisibleTarget.name);
                target = closestVisibleTarget;
                TransitionToChase();
            }
            else if (currentState == BotState.Search) // Se estava a procurar e Re-viu o mesmo alvo
            {
                 // Debug.Log(gameObject.name + " RE-DETECTED Target while Searching: " + closestVisibleTarget.name);
                 TransitionToChase(); // Volta a perseguir
            }
            // Se já estava a perseguir o mesmo alvo, não faz nada (DoChase continua)
        }
        // Se NÃO encontrou nenhum alvo visível E estava a perseguir, o timer do DoChase tratará disso.
        // Se NÃO encontrou nenhum alvo visível E estava a procurar, o timer do DoSearch tratará disso.
        // Se estava a patrulhar e não encontrou nada, continua a patrulhar.
    }


    private bool HasLineOfSight(Transform t)
    {
        if (t == null) return false;

        // Origem do Raycast: Um pouco acima e à frente do bot
        Vector3 eyesPosition = transform.position + Vector3.up * 1.6f + transform.forward * 0.3f;
        // Destino do Raycast: Centro do collider do alvo, se possível
        Vector3 targetCenter = t.position + Vector3.up * 1.0f; // Default: 1m acima do pivot
        Collider targetCollider = t.GetComponentInChildren<Collider>(); // Tenta encontrar um collider
        if (targetCollider) targetCenter = targetCollider.bounds.center; // Usa o centro do collider se existir

        Vector3 direction = targetCenter - eyesPosition;
        float distance = direction.magnitude;

        // // Debug Visual: Linha de visão a ser testada
        // Debug.DrawRay(eyesPosition, direction.normalized * distance, Color.yellow);

        // Faz o Raycast, ignorando a layer do próprio bot (se necessário)
        if (Physics.Raycast(eyesPosition, direction.normalized, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // Acertou em algo. Verifica se NÃO é o alvo.
            if (hit.transform.root != t.root) // Compara as raízes para abranger alvos com múltiplos colliders
            {
                // // Debug: O que bloqueou a visão
                // Debug.DrawLine(eyesPosition, hit.point, Color.red, 0.1f);
                // Debug.Log(gameObject.name + " LOS to " + t.name + " blocked by: " + hit.collider.name);
                return false; // Visão bloqueada por um obstáculo
            }
        }

        // // Debug: Linha verde se a visão estiver limpa (ou se acertou no próprio alvo)
        // Debug.DrawRay(eyesPosition, direction.normalized * distance, Color.green);
        return true; // Visão limpa
    }

    private void UpdateAnimation()
    {
        if (!animator || !agent || !agent.isActiveAndEnabled)
        {
            if (animator) animator.SetFloat("Speed", 0f); // Garante reset da animação
            return;
        }

        // Usa a velocidade desejada para a animação parecer mais responsiva
        Vector3 desiredVelocity = agent.desiredVelocity;
        // Converte a velocidade global para local (relativa à direção do bot)
        Vector3 localDesiredVel = transform.InverseTransformDirection(desiredVelocity);

        // A velocidade para a animação é a componente "para a frente" (Z local)
        // Normaliza dividindo pela velocidade máxima do agente
        float speed = localDesiredVel.z / agent.speed;

        // Garante que o valor está entre 0 (parado/andar para trás) e 1 (velocidade máxima para a frente)
        // O Animator normalmente só precisa de saber se está a andar para a frente.
        speed = Mathf.Clamp01(speed);

        // Envia o valor para o Animator com suavização (0.1f = tempo para atingir o valor)
        animator.SetFloat("Speed", speed * animationSpeedMultiplier, 0.1f, Time.deltaTime);
    }
}