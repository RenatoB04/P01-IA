using UnityEngine;
using UnityEngine.AI;

public class BotAI_Proto : MonoBehaviour
{
    public Transform[] patrolPoints;
    public float waypointTolerance = 0.6f;

    [Header("Visão")]
    public float viewRadius = 15f;
    [Range(0,180)] public float viewAngle = 110f;
    public LayerMask targetMask;    // Player
    public LayerMask obstacleMask;  // Environment
    public float loseSightTime = 2f;

    NavMeshAgent agent;
    Transform target;
    int idx;
    float loseTimer;

    void Awake(){
        agent = GetComponent<NavMeshAgent>();
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update(){
        DetectPlayer();

        if (target) {
            // CHASE
            agent.stoppingDistance = 2f;
            agent.SetDestination(target.position);

            if (!HasLineOfSight(target)) {
                loseTimer += Time.deltaTime;
                if (loseTimer > loseSightTime) { target = null; loseTimer = 0f; }
            } else loseTimer = 0f;
        }
        else {
            // PATROL
            agent.stoppingDistance = 0f;
            if (!agent.pathPending && agent.remainingDistance < waypointTolerance && patrolPoints.Length > 0){
                idx = (idx + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[idx].position);
            }
        }
    }

    void DetectPlayer(){
        // só procura se não tiver target
        if (target) return;

        var hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
        foreach (var h in hits){
            var cand = h.transform;
            Vector3 dir = (cand.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) <= viewAngle * 0.5f && HasLineOfSight(cand)){
                target = cand;
                break;
            }
        }
    }

    bool HasLineOfSight(Transform t){
        Vector3 eyes = transform.position + Vector3.up * 1.2f;
        Vector3 dest = t.position + Vector3.up * 1.2f;
        Vector3 dir = dest - eyes;
        if (Physics.Raycast(eyes, dir.normalized, out var hit, dir.magnitude))
        {
            // se bateu noutro collider antes do target e esse collider está na obstacleMask, bloqueia
            if (hit.transform != t && ((1 << hit.collider.gameObject.layer) & obstacleMask) != 0) return false;
        }
        return true;
    }
}
