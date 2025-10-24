using UnityEngine;
using UnityEngine.AI;

public class BotCombat : MonoBehaviour
{
    [Header("Refs (preenche no Inspector)")]
    public Weapon weapon;                 // arma do bot
    public Transform shootPoint;          // ponta da arma / firePoint
    public Transform eyes;                // ponto dos "olhos" (1.2–1.6m)

    [Header("Alvo")]
    public string playerTag = "Player";
    public LayerMask playerLayer;         // layer do Player

    [Header("Visão / Tiro")]
    [Range(30f, 180f)] public float fovDegrees = 130f;   // cone de visão (total)
    public float attackRange = 25f;                       // alcance máximo
    public float stopDistance = 8f;                       // distância de “paragem/combate”
    public float aimTurnSpeed = 360f;                     // °/s para virar o corpo
    [Range(1f, 45f)] public float maxShootAngle = 12f;    // só atira se estiver quase virado
    public float fireCooldown = 0.8f;                     // para atirar mais devagar
    public float aimHeightOffset = 1.1f;                  // ponto onde mira no player (peito)

    [Header("LOS / Obstáculos")]
    public LayerMask obstacleMask = ~0;   // layers que bloqueiam visão (NÃO incluir player)

    [Header("Debug")]
    public bool debugLogs = false;
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
        
        // Fallback por Layer (procura qualquer objeto com Health na layer do player)
        if (!player && playerLayer.value != 0)
        {
            var all = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (var h in all)
            {
                if (((1 << h.gameObject.layer) & playerLayer.value) != 0)
                {
                    player = h.transform;
                    break;
                }
            }
        }

        // Refs da arma
        if (!weapon) weapon = GetComponentInChildren<Weapon>(true);
        if (!shootPoint && weapon) shootPoint = weapon.firePoint;
        if (!eyes) eyes = transform;

        if (debugLogs)
            Debug.Log($"[BotCombat] init: weapon={(weapon?weapon.name:"null")}, shootPoint={(shootPoint?shootPoint.name:"null")}, player={(player?player.name:"null")}");
    }

    void Update()
    {
        if (!weapon || !shootPoint || !player) return;

        // Distância & ponto de mira no player
        Vector3 aimPoint = player.position + Vector3.up * aimHeightOffset;
        float dist = Vector3.Distance(transform.position, aimPoint);
        if (dist > attackRange) { if (debugLogs) Debug.Log("[BotCombat] muito longe"); return; }

        // Tem de estar suficientemente perto do ponto de combate
        bool closeEnough = dist <= Mathf.Max(stopDistance + 0.5f, stopDistance * 1.05f);
        if (!closeEnough) { if (debugLogs) Debug.Log("[BotCombat] a aproximar"); return; }

        // Verificar se o alvo está dentro do FOV (frente do bot, plano XZ)
        Vector3 to = (aimPoint - transform.position);
        Vector3 toFlat = to; toFlat.y = 0f; toFlat.Normalize();
        Vector3 fwdFlat = transform.forward; fwdFlat.y = 0f; fwdFlat.Normalize();

        float angleToTarget = Vector3.Angle(fwdFlat, toFlat);
        float halfFov = fovDegrees * 0.5f;
        bool inFov = angleToTarget <= halfFov;
        if (!inFov)
        {
            // vira o corpo na direção do alvo, mas não dispara fora do FOV
            RotateBodyTowards(toFlat);
            if (drawRays) Debug.DrawRay(transform.position, fwdFlat * 1.5f, Color.yellow);
            return;
        }

        // LOS real: raycast dos olhos até ao alvo; só conta se o PRIMEIRO hit for o player
        Vector3 eyesPos = eyes.position;
        Vector3 dirEyes = (aimPoint - eyesPos);
        float rayLen = dirEyes.magnitude;

        // Monta a máscara: obstáculos + layer do player (para podermos acertar no player)
        int playerLayerIndex = GetSingleLayerIndex(playerLayer);
        LayerMask losMask = obstacleMask;
        if (playerLayerIndex >= 0) losMask |= (1 << playerLayerIndex);

        bool hitSomething = Physics.Raycast(eyesPos, dirEyes.normalized, out var hit, rayLen, losMask, QueryTriggerInteraction.Ignore);
        if (drawRays) Debug.DrawRay(eyesPos, dirEyes.normalized * Mathf.Min(rayLen, 3f), hitSomething ? Color.red : Color.green);

        bool hasClearLOS = hitSomething && hit.transform == player; // PRIMEIRO contacto tem de ser o player
        if (!hasClearLOS)
        {
            if (debugLogs) Debug.Log($"[BotCombat] sem LOS (parede/obstáculo entre bot e player)");
            // vira o corpo para o alvo mesmo assim
            RotateBodyTowards(toFlat);
            return; // >>> NÃO dispara nem tenta apontar para a parede <<<
        }

        // Virar o corpo no plano XZ em direção ao player
        RotateBodyTowards(toFlat);

        // Só dispara quando estiver suficientemente alinhado (ângulo pequeno)
        bool wellAligned = angleToTarget <= maxShootAngle;

        // Roda apenas o cano da arma para "fino ajuste"
        if (wellAligned)
        {
            Vector3 from = shootPoint.position;
            Vector3 dir = (aimPoint - from);
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                shootPoint.rotation = Quaternion.Slerp(shootPoint.rotation, look, Time.deltaTime * aimTurnSpeed);
            }
        }

        // Disparo com cooldown, só se alinhado E com LOS limpo
        if (wellAligned && Time.time >= nextFireTime)
        {
            weapon.ShootExternally();
            nextFireTime = Time.time + fireCooldown;
            if (debugLogs) Debug.Log("[BotCombat] SHOOT");
        }
    }

    void RotateBodyTowards(Vector3 toFlatDir)
    {
        if (toFlatDir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(toFlatDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, aimTurnSpeed * Time.deltaTime);
    }

    // Retorna o índice da única layer setada em playerLayer; se houver múltiplas, retorna -1.
    int GetSingleLayerIndex(LayerMask lm)
    {
        int mask = lm.value;
        if (mask == 0) return -1;
        // se tiver mais de 1 bit, considera inválido
        if ((mask & (mask - 1)) != 0) return -1;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) return i;
        return -1;
    }
}