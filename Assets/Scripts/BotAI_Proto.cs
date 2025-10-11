using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotAI_Proto : MonoBehaviour
{
    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float waypointTolerance = 0.6f;

    [Header("Visão")]
    public float viewRadius = 15f;
    [Range(0, 180)] public float viewAngle = 110f;
    public LayerMask targetMask;
    public LayerMask obstacleMask;
    public float loseSightTime = 2f;

    [Header("Animação")]
    public Animator animator;
    [Tooltip("Multiplicador de velocidade para sincronizar a animação com o movimento real.")]
    public float speedMultiplier = 1.0f;

    NavMeshAgent agent;
    Transform target;
    int idx;
    float loseTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);

        // tenta encontrar automaticamente o Animator no filho (por ex. o Y-Bot)
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        // garante que o Root Motion está desligado (NavMeshAgent controla o movimento)
        if (animator)
            animator.applyRootMotion = false;
    }

    void Update()
    {
        DetectPlayer();

        if (target)
        {
            // CHASE
            agent.stoppingDistance = 2f;
            agent.SetDestination(target.position);

            if (!HasLineOfSight(target))
            {
                loseTimer += Time.deltaTime;
                if (loseTimer > loseSightTime)
                {
                    target = null;
                    loseTimer = 0f;
                }
            }
            else loseTimer = 0f;
        }
        else
        {
            // PATROL
            agent.stoppingDistance = 0f;
            if (!agent.pathPending && agent.remainingDistance < waypointTolerance && patrolPoints.Length > 0)
            {
                idx = (idx + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[idx].position);
            }
        }

        UpdateAnimation();
    }

    void UpdateAnimation()
    {
        if (!animator || !agent) return;

        // velocidade horizontal (sem componente Y)
        Vector3 vel = agent.velocity;
        vel.y = 0f;

        float speed = vel.magnitude * speedMultiplier;
        animator.SetFloat("Speed", speed);

        // (opcional) adicionar outros parâmetros, como "IsChasing"
        // animator.SetBool("IsChasing", target != null);
    }

    void DetectPlayer()
    {
        if (target) return;

        var hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
        foreach (var h in hits)
        {
            var cand = h.transform;
            Vector3 dir = (cand.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) <= viewAngle * 0.5f && HasLineOfSight(cand))
            {
                target = cand;
                break;
            }
        }
    }

    bool HasLineOfSight(Transform t)
    {
        Vector3 eyes = transform.position + Vector3.up * 1.2f;
        Vector3 dest = t.position + Vector3.up * 1.2f;
        Vector3 dir = dest - eyes;

        if (Physics.Raycast(eyes, dir.normalized, out var hit, dir.magnitude))
        {
            if (hit.transform != t && ((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                return false;
        }
        return true;
    }
}
