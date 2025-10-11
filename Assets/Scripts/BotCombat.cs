using UnityEngine;
using UnityEngine.AI;

public class BotCombat : MonoBehaviour
{
    [Header("Refs (preenche no Inspector)")]
    public Weapon weapon;                 // arma do bot (o teu script)
    public Transform shootPoint;          // FirePoint da arma do bot

    [Header("Alvo")]
    public string playerTag = "Player";
    public LayerMask playerLayer;

    [Header("Combate")]
    public float attackRange = 25f;
    public float stopDistance = 8f;
    public float aimTurnSpeed = 20f;
    public float fireCooldown = 0.05f;

    [Header("LOS / Obstáculos")]
    public float aimHeightOffset = 1.1f;
    public LayerMask obstacleMask = ~0;

    [Header("Debug")]
    public bool debugLogs = true;
    public bool drawRays = true;

    NavMeshAgent agent;
    Transform player;
    float nextFireTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Encontrar Player por Tag
        if (!string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }
        // Fallback por Layer
        if (!player && playerLayer.value != 0)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_2022_2_OR_NEWER
            var all = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<Health>(true);
#endif
            foreach (var h in all)
            {
                if (((1 << h.gameObject.layer) & playerLayer.value) != 0) { player = h.transform; break; }
            }
        }

        // Refs da arma
        if (!weapon) weapon = GetComponentInChildren<Weapon>(true);
        if (!shootPoint && weapon) shootPoint = weapon.firePoint;

        if (debugLogs)
        {
            Debug.Log($"[BotCombat] init: weapon={(weapon?weapon.name:"null")}, shootPoint={(shootPoint?shootPoint.name:"null")}, player={(player?player.name:"null")}");
        }
    }

    void Update()
    {
        if (!weapon || !shootPoint || !player) return;

        var aimPoint = player.position + Vector3.up * aimHeightOffset;
        float dist = Vector3.Distance(transform.position, aimPoint);

        if (dist > attackRange)
        {
            if (debugLogs) Debug.Log("[BotCombat] muito longe");
            return;
        }

        // Tem de estar próximo do ponto onde a IA pára
        bool closeEnough = dist <= Mathf.Max(stopDistance + 0.5f, stopDistance * 1.05f);
        if (!closeEnough)
        {
            if (debugLogs) Debug.Log("[BotCombat] ainda a aproximar (fora do stopDistance)");
            return;
        }

        // Certificar que não estamos a bloquear o LOS com a Layer Player por engano
        LayerMask losMask = obstacleMask;
        if (player) losMask &= ~(1 << player.gameObject.layer);

        Vector3 from = shootPoint.position;
        Vector3 dir = (aimPoint - from);
        float rayLen = dir.magnitude;

        bool blocked = Physics.Raycast(from, dir.normalized, out var hit, rayLen, losMask, QueryTriggerInteraction.Ignore);
        if (drawRays) Debug.DrawRay(from, dir.normalized * Mathf.Min(rayLen, 3f), blocked ? Color.red : Color.green);

        if (blocked)
        {
            if (debugLogs) Debug.Log($"[BotCombat] sem LOS, bate em: {hit.collider.name}");
            // Mesmo bloqueado, vamos apontar para o ponto de impacto (parede)
            dir = (hit.point - from);
        }

        // Rodar cano
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            shootPoint.rotation = Quaternion.Slerp(shootPoint.rotation, look, Time.deltaTime * aimTurnSpeed);
        }

        // Disparo
        if (Time.time >= nextFireTime)
        {
            weapon.ShootExternally();         // chama a tua arma
            nextFireTime = Time.time + fireCooldown;
            if (debugLogs) Debug.Log("[BotCombat] SHOOT");
        }
    }
}
