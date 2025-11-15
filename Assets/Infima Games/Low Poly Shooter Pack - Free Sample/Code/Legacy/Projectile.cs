using UnityEngine;
using System.Collections;
using Unity.Netcode; // ADIÇÃO: Necessário para o Netcode
using InfimaGames.LowPolyShooterPack;
using Random = UnityEngine.Random;

// ALTERAÇÃO: Herda de NetworkBehaviour
public class Projectile : NetworkBehaviour
{
    [Range(5, 100)]
    [Tooltip("After how long time should the bullet prefab be destroyed?")]
    public float destroyAfter = 10f;

    [Tooltip("If enabled the bullet destroys on impact")]
    public bool destroyOnImpact = false;

    [Tooltip("Minimum time after impact that the bullet is destroyed")]
    public float minDestroyTime = 0.05f;

    [Tooltip("Maximum time after impact that the bullet is destroyed")]
    public float maxDestroyTime = 0.25f;

    [Header("Damage")]
    [Tooltip("Dano base aplicado ao Health quando acerta num alvo.")]
    [SerializeField] private float damage = 20f;

    // CAMPOS DE REDE (Injetados pelo Servidor/RPC)
    [Header("Network Data")]
    [HideInInspector] public ulong ownerClientId = ulong.MaxValue; // Client que disparou (definido no PlayerWeaponController)
    [HideInInspector] public int ownerTeam = -1;                   // Equipa do atirador
    public NetworkVariable<Vector3> initialVelocity = new NetworkVariable<Vector3>(Vector3.zero);

    // IMPACTOS DO KIT
    [Header("Impact Effect Prefabs")]
    public Transform[] bloodImpactPrefabs;
    public Transform[] metalImpactPrefabs;
    public Transform[] dirtImpactPrefabs;
    public Transform[] concreteImpactPrefabs;

    private Rigidbody rb;
    private Collider projectileCollider;

    // =====================================================================
    //                            NETWORK SPAWN
    // =====================================================================
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();

        if (rb == null || projectileCollider == null)
        {
            Debug.LogError("Projectile: Falta Rigidbody ou Collider no prefab da bala.");
            if (IsSpawned) NetworkObject.Despawn(true);
            else Destroy(gameObject);
            return;
        }

        // Aplicar a velocidade inicial vinda do servidor
        if (initialVelocity.Value != Vector3.zero)
            rb.linearVelocity = initialVelocity.Value;

        // Ignorar colisão com o jogador que disparou (feito no SERVIDOR, que é quem simula a física)
        if (IsServer && ownerClientId != ulong.MaxValue && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client) &&
                client != null && client.PlayerObject != null)
            {
                if (client.PlayerObject.TryGetComponent<Collider>(out var playerCollider))
                {
                    Physics.IgnoreCollision(playerCollider, projectileCollider, true);
                }
            }
        }

        // Timer de auto-destruição
        StartCoroutine(DestroyAfter());
    }

    // =====================================================================
    //                        PROCESSAMENTO DE IMPACTO
    // =====================================================================

    /// <summary>
    /// Função comum para tratar impacto (dano + efeitos).
    /// Só é executada no Servidor.
    /// </summary>
    private void ProcessHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Só o servidor trata de dano e destruição
        if (!IsServer)
            return;

        // Ignorar outras balas
        if (hitObject.GetComponent<Projectile>() != null)
            return;

        // ---------- DANO ----------
        // Procura um Health em cima na hierarquia (útil quando o collider está num filho, ex.: BlueSoldier_Male/Hitbox_Body)
        var healthComponent = hitObject.GetComponentInParent<Health>();
        if (healthComponent != null)
        {
            // Chama o núcleo server-authoritative do teu Health
            healthComponent.ApplyDamageServer(
                damage,
                ownerTeam,
                ownerClientId,
                hitPoint,
                true
            );
        }

        // ---------- EFEITOS VISUAIS ----------
        string tag = hitObject.tag;

        if (tag == "Blood" && bloodImpactPrefabs.Length > 0)
        {
            Instantiate(
                bloodImpactPrefabs[Random.Range(0, bloodImpactPrefabs.Length)],
                hitPoint,
                Quaternion.LookRotation(hitNormal));
            DespawnSelf();
            return;
        }

        if (tag == "Metal" && metalImpactPrefabs.Length > 0)
        {
            Instantiate(
                metalImpactPrefabs[Random.Range(0, metalImpactPrefabs.Length)],
                hitPoint,
                Quaternion.LookRotation(hitNormal));
            DespawnSelf();
            return;
        }

        if (tag == "Dirt" && dirtImpactPrefabs.Length > 0)
        {
            Instantiate(
                dirtImpactPrefabs[Random.Range(0, dirtImpactPrefabs.Length)],
                hitPoint,
                Quaternion.LookRotation(hitNormal));
            DespawnSelf();
            return;
        }

        if (tag == "Concrete" && concreteImpactPrefabs.Length > 0)
        {
            Instantiate(
                concreteImpactPrefabs[Random.Range(0, concreteImpactPrefabs.Length)],
                hitPoint,
                Quaternion.LookRotation(hitNormal));
            DespawnSelf();
            return;
        }

        if (tag == "Target")
        {
            var target = hitObject.GetComponent<TargetScript>();
            if (target != null) target.isHit = true;
            DespawnSelf();
            return;
        }

        if (tag == "ExplosiveBarrel")
        {
            var barrel = hitObject.GetComponent<ExplosiveBarrelScript>();
            if (barrel != null) barrel.explode = true;
            DespawnSelf();
            return;
        }

        if (tag == "GasTank")
        {
            var gas = hitObject.GetComponent<GasTankScript>();
            if (gas != null) gas.isHit = true;
            DespawnSelf();
            return;
        }

        // Se chegou aqui, foi impacto genérico.
        if (destroyOnImpact)
            DespawnSelf();
        else
            StartCoroutine(DestroyTimer());
    }

    // Colisão clássica (quando ambos os colliders NÃO são trigger)
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            var contact = collision.contacts[0];
            ProcessHit(collision.gameObject, contact.point, contact.normal);
        }
        else
        {
            ProcessHit(collision.gameObject, transform.position, -rb.linearVelocity.normalized);
        }
    }

    // Trigger (quando pelo menos um dos colliders é IsTrigger = true)
    private void OnTriggerEnter(Collider other)
    {
        // Ponto/normal aproximados para triggers (não há contacts)
        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = rb != null && rb.linearVelocity != Vector3.zero
            ? -rb.linearVelocity.normalized
            : Vector3.up;

        ProcessHit(other.gameObject, hitPoint, hitNormal);
    }

    // =====================================================================
    //                           DESTRUIÇÃO / TIMERS
    // =====================================================================

    private void DespawnSelf()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    private IEnumerator DestroyTimer()
    {
        yield return new WaitForSeconds(Random.Range(minDestroyTime, maxDestroyTime));
        DespawnSelf();
    }

    private IEnumerator DestroyAfter()
    {
        yield return new WaitForSeconds(destroyAfter);
        DespawnSelf();
    }
}
