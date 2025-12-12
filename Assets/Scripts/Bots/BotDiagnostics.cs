using System;
using UnityEngine;





[DisallowMultipleComponent]
public class BotDiagnostics : MonoBehaviour
{
    [Tooltip("Se true, mostra logs detalhados sobre colisões e mudanças de vida.")]
    public bool verbose = true;

    private Health health;
    private Collider anyCollider;
    private Rigidbody anyRigidbody;
    private float lastHealthValue = float.MinValue;
    private string id;

    void Awake()
    {
        id = $"{gameObject.name}#{GetInstanceID()}";
        health = GetComponentInChildren<Health>() ?? GetComponent<Health>();
        anyCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        anyRigidbody = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        Debug.Log($"[BotDiagnostics] ({id}) Awake. health={(health!=null)}, collider={(anyCollider!=null)}, rb={(anyRigidbody!=null)}, layer={gameObject.layer}.");
    }

    void Start()
    {
        if (health != null)
        {
            lastHealthValue = health != null ? health.currentHealth.Value : float.NaN;
            if (verbose) Debug.Log($"[BotDiagnostics] ({id}) Start: HP inicial = {lastHealthValue}");
        }
        else
        {
            Debug.LogWarning($"[BotDiagnostics] ({id}) Start: Health NÃO encontrado no bot. GetComponentInChildren<Health() retornou null.");
        }
    }

    void Update()
    {
        
        if (health != null)
        {
            float curr = health.currentHealth.Value;
            if (!Mathf.Approximately(curr, lastHealthValue))
            {
                Debug.Log($"[BotDiagnostics] ({id}) HP mudou: {lastHealthValue} -> {curr}");
                lastHealthValue = curr;
            }
        }
    }

    
    void OnCollisionEnter(Collision collision)
    {
        if (!verbose) return;
        var col = collision.collider;
        LogCollision("OnCollisionEnter", col, collision.contacts.Length > 0 ? collision.GetContact(0).point : (Vector3?)null);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!verbose) return;
        LogCollision("OnTriggerEnter", other, null);
    }

    private void LogCollision(string evt, Collider col, Vector3? hitPoint)
    {
        string rootName = col.transform.root ? col.transform.root.name : "null";
        string colliderName = col.name;
        string layerName = LayerMask.LayerToName(col.gameObject.layer);
        string s = $"[BotDiagnostics] ({id}) {evt}: collider={colliderName} root={rootName} layer={col.gameObject.layer}({layerName})";
        if (hitPoint.HasValue) s += $" hitPos={hitPoint.Value}";
        Debug.Log(s);

        
        var bullet = col.GetComponentInParent<BulletProjectile>() ?? col.GetComponentInChildren<BulletProjectile>();
        if (bullet != null)
        {
            
            int ownerTeam = -999;
            try { ownerTeam = bullet.ownerTeam; } catch { }
            var ownerRootName = bullet.ownerRoot ? bullet.ownerRoot.name : "null";
            Debug.Log($"[BotDiagnostics] ({id}) Colidido por BulletProjectile: ownerClientId={bullet.ownerClientId}, ownerTeam={ownerTeam}, ownerRoot={ownerRootName}, damage={bullet.damage}, initialVelocity={bullet.initialVelocity.Value}");
        }

        
    }

    
    [ContextMenu("DumpHealthState")]
    public void DumpHealthState()
    {
        if (health == null)
        {
            Debug.Log($"[BotDiagnostics] ({id}) DumpHealthState: Health null.");
            return;
        }
        Debug.Log($"[BotDiagnostics] ({id}) DumpHealthState: currentHealth={health.currentHealth.Value} maxHealth={health.maxHealth} isDead={health.isDead.Value} team={health.team.Value}");
    }
}