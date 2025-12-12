using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BulletProjectile : NetworkBehaviour
{
    [Header("Dano")]
    public float damage = 20f;

    [Header("Vida útil")]
    public float lifeTime = 5f;

    [Header("Filtro (opcional)")]
    [Tooltip("Layers que o projéctil pode atingir. Por defeito: todas (~0).")]
    public LayerMask hittableLayers = ~0;

    [HideInInspector] public int   ownerTeam     = -1;
    [HideInInspector] public Transform ownerRoot = null;
    [HideInInspector] public ulong ownerClientId = ulong.MaxValue; 

    
    public NetworkVariable<Vector3> initialVelocity = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool hasHit = false;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        var col = GetComponent<Collider>();
        if (col) col.enabled = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer && rb != null)
        {
            if (initialVelocity.Value != Vector3.zero)
                rb.linearVelocity = initialVelocity.Value;
        }

        if (IsServer)
            Invoke(nameof(ServerLifetimeEnd), lifeTime);
    }

    void ServerLifetimeEnd()
    {
        if (!IsServer) return;
        var no = GetComponent<NetworkObject>();
        if (no && no.IsSpawned) no.Despawn();
        else Destroy(gameObject);
    }

    void OnCollisionEnter(Collision c)
    {
        if (!IsServer) return;
        if (hasHit) return;

        
        if (ownerRoot && c.transform.root == ownerRoot)
        {
            Debug.Log($"[Bullet] Ignorado (collision): colisão com ownerRoot ({ownerRoot.name}). collider={c.collider.name}");
            return;
        }

        
        
        

        if (((1 << c.gameObject.layer) & hittableLayers) == 0)
        {
            Debug.Log($"[Bullet] Ignorado (collision): layer {c.gameObject.layer} não está em hittableLayers.");
            ServerCleanup();
            return;
        }

        Vector3 hitPos = transform.position;
        if (c.contactCount > 0) hitPos = c.GetContact(0).point;
        ProcessHitServer(c.collider, hitPos);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (hasHit) return;

        
        if (ownerRoot && other.transform.root == ownerRoot)
        {
            Debug.Log($"[Bullet] Ignorado (trigger): colisão com ownerRoot ({ownerRoot.name}). collider={other.name}");
            return;
        }

        

        if (((1 << other.gameObject.layer) & hittableLayers) == 0)
        {
            Debug.Log($"[Bullet] Ignorado (trigger): layer {other.gameObject.layer} não está em hittableLayers.");
            ServerCleanup();
            return;
        }

        ProcessHitServer(other, transform.position);
    }

    private void ProcessHitServer(Collider col, Vector3 hitPos)
    {
        if (hasHit) return;
        hasHit = true;

        Debug.Log($"[Bullet] ProcessHitServer: collider={col.name}, root={col.transform.root.name}, layer={col.gameObject.layer}, ownerClientId={ownerClientId}, ownerTeam={ownerTeam}, ownerRoot={(ownerRoot? ownerRoot.name : "null")}");

        
        var targetHealth = col.GetComponentInParent<Health>();

        
        if (targetHealth == null)
        {
            var root = col.transform.root;
            targetHealth = root.GetComponentInChildren<Health>(true);
            if (targetHealth != null)
            {
                Debug.Log($"[Bullet] Health encontrado via GetComponentInChildren no root '{root.name}' -> health on '{targetHealth.name}'.");
            }
        }

        
        if (targetHealth == null)
        {
            Collider[] nearby = Physics.OverlapSphere(hitPos, 0.25f, hittableLayers, QueryTriggerInteraction.Ignore);
            Debug.Log($"[Bullet] OverlapSphere fallback: encontrou {nearby.Length} colliders próximos.");
            foreach (var nc in nearby)
            {
                var hh = nc.GetComponentInParent<Health>() ?? nc.GetComponentInChildren<Health>(true);
                if (hh != null)
                {
                    
                    if (nc.transform.root == col.transform.root)
                    {
                        targetHealth = hh;
                        Debug.Log($"[Bullet] Escolhido Health preferencial (mesmo root) = {hh.name} (via collider {nc.name}).");
                        break;
                    }
                    
                    if (targetHealth == null)
                    {
                        targetHealth = hh;
                        Debug.Log($"[Bullet] Candidate Health (via OverlapSphere) = {hh.name} (collider {nc.name}).");
                    }
                }
            }
        }

        if (targetHealth == null)
        {
            Debug.Log($"[Bullet] No Health found on collided object {col.name} (root={col.transform.root.name}). Não apliquei dano.");
            ServerCleanup();
            return;
        }

        
        bool applied = TryApplyDamageTo(targetHealth, hitPos);
        if (!applied)
        {
            Debug.Log($"[Bullet] Dano NÃO aplicado a {targetHealth.name}. Razão nos logs acima.");
        }

        ServerCleanup();
    }

    private bool TryApplyDamageTo(Health h, Vector3 hitPos)
    {
        if (h == null) return false;

        
        if (ownerRoot != null && h.transform.root == ownerRoot)
        {
            Debug.Log($"[Bullet] ApplyDamage skipped: target root == ownerRoot ({ownerRoot.name})");
            return false;
        }

        
        int targetTeam = h != null ? h.team.Value : -1;
        int instigatorTeam = ownerTeam;
        if (targetTeam != -1 && instigatorTeam != -1 && targetTeam == instigatorTeam)
        {
            Debug.Log($"[Bullet] ApplyDamage skipped por Friendly Fire: targetTeam={targetTeam}, instigatorTeam={instigatorTeam}");
            return false;
        }

        
        try
        {
            h.ApplyDamageServer(damage, instigatorTeam, ownerClientId, hitPos, true);
            Debug.Log($"[Bullet] Applied {damage} to {h.name} (team target={h.team.Value}, team owner={instigatorTeam}, ownerClientId={ownerClientId})");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Bullet] Exception applying damage to {h.name}: {ex}");
            return false;
        }
    }

    [ClientRpc]
    void HitmarkerClientRpc(float dealt, string victimName, ClientRpcParams rpcParams = default)
    {
        if (DamageFeedUI.Instance)
            DamageFeedUI.Instance.Push(dealt, false, victimName);
        CrosshairUI.Instance?.ShowHit();
    }

    private void ServerCleanup()
    {
        if (!IsServer) return;
        var no = GetComponent<NetworkObject>();
        if (no && no.IsSpawned) no.Despawn();
        else Destroy(gameObject);
    }
}