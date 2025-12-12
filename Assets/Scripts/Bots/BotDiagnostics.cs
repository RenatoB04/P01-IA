using System;
using UnityEngine;

[DisallowMultipleComponent] // Garante que apenas um componente deste tipo existe por GameObject
public class BotDiagnostics : MonoBehaviour
{
    [Tooltip("Se true, mostra logs detalhados sobre colisões e mudanças de vida.")]
    public bool verbose = true; // Ativa ou desativa logs detalhados

    private Health health; // Referência ao script de vida do bot
    private Collider anyCollider; // Collider principal do bot
    private Rigidbody anyRigidbody; // Rigidbody principal do bot
    private float lastHealthValue = float.MinValue; // Último valor de vida guardado
    private string id; // Identificador único do bot para logs

    void Awake()
    {
        // Cria um ID único baseado no nome do GameObject e InstanceID
        id = $"{gameObject.name}#{GetInstanceID()}";

        // Tenta obter componentes essenciais
        health = GetComponentInChildren<Health>() ?? GetComponent<Health>();
        anyCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        anyRigidbody = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        // Log inicial de diagnóstico
        Debug.Log($"[BotDiagnostics] ({id}) Awake. health={(health!=null)}, collider={(anyCollider!=null)}, rb={(anyRigidbody!=null)}, layer={gameObject.layer}.");
    }

    void Start()
    {
        // Inicializa o último valor de vida
        if (health != null)
        {
            lastHealthValue = health.currentHealth.Value;
            if (verbose) Debug.Log($"[BotDiagnostics] ({id}) Start: HP inicial = {lastHealthValue}");
        }
        else
        {
            Debug.LogWarning($"[BotDiagnostics] ({id}) Start: Health NÃO encontrado no bot. GetComponentInChildren<Health() retornou null.");
        }
    }

    void Update()
    {
        // Verifica alterações de vida a cada frame
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
        if (!verbose) return; // Se logs desativados, ignora
        var col = collision.collider;
        LogCollision("OnCollisionEnter", col, collision.contacts.Length > 0 ? collision.GetContact(0).point : (Vector3?)null);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!verbose) return; // Ignora se logs desativados
        LogCollision("OnTriggerEnter", other, null);
    }

    // Função central para logar informações sobre colisões
    private void LogCollision(string evt, Collider col, Vector3? hitPoint)
    {
        string rootName = col.transform.root ? col.transform.root.name : "null";
        string colliderName = col.name;
        string layerName = LayerMask.LayerToName(col.gameObject.layer);
        string s = $"[BotDiagnostics] ({id}) {evt}: collider={colliderName} root={rootName} layer={col.gameObject.layer}({layerName})";
        if (hitPoint.HasValue) s += $" hitPos={hitPoint.Value}";
        Debug.Log(s);

        // Se a colisão foi com um projétil, mostra informações detalhadas
        var bullet = col.GetComponentInParent<BulletProjectile>() ?? col.GetComponentInChildren<BulletProjectile>();
        if (bullet != null)
        {
            int ownerTeam = -999;
            try { ownerTeam = bullet.ownerTeam; } catch { }
            var ownerRootName = bullet.ownerRoot ? bullet.ownerRoot.name : "null";
            Debug.Log($"[BotDiagnostics] ({id}) Colidido por BulletProjectile: ownerClientId={bullet.ownerClientId}, ownerTeam={ownerTeam}, ownerRoot={ownerRootName}, damage={bullet.damage}, initialVelocity={bullet.initialVelocity.Value}");
        }
    }

    // Função de debug que permite inspecionar estado da vida via ContextMenu
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
