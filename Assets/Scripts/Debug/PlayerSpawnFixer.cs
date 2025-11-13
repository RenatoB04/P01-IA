using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

[DisallowMultipleComponent]
public class PlayerSpawnFixer : NetworkBehaviour
{
    [Tooltip("Se true tenta corrigir posição errada logo após spawn (recomendado).")]
    public bool enableFix = true;

    [Tooltip("Altura mínima aceitável; se spawn abaixo disso tenta corrigi-lo.")]
    public float minAcceptY = -1f;

    [Tooltip("Altura fallback para teleporte se não houver SpawnPoint/NavMesh.")]
    public float fallbackY = 1.5f;

    [Tooltip("Número de tentativas (frames) para aplicar a correção.")]
    public int attempts = 6;

    [Tooltip("Delay inicial antes da primeira tentativa (s).")]
    public float initialDelay = 0.03f;

    void Start()
    {
        // nada
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!enableFix) return;

        // Só o servidor deverá fazer a correção de posição do objeto spawnado
        if (IsServer)
        {
            Debug.Log($"[PlayerSpawnFixer] OnNetworkSpawn owner={OwnerClientId} pos={transform.position}");
            StartCoroutine(FixSpawnCoroutine());
        }
    }

    IEnumerator FixSpawnCoroutine()
    {
        // espera um pouco para permitir outros sistemas correrem
        yield return new WaitForSeconds(initialDelay);

        for (int i = 0; i < attempts; i++)
        {
            Vector3 before = transform.position;

            // defesa: remove parent (algum sistema pode parentar temporariamente)
            if (transform.parent != null)
            {
                Debug.Log($"[PlayerSpawnFixer] Removing parent '{transform.parent.name}' before teleport.");
                transform.SetParent(null);
            }

            // Se já está acima do limiar consideramos OK
            if (before.y > minAcceptY && i == 0)
            {
                Debug.Log($"[PlayerSpawnFixer] Spawn OK (y={before.y}) on first check.");
                yield break;
            }

            // Tenta teleport inteligente: SpawnPoint tag
            var spawns = GameObject.FindGameObjectsWithTag("SpawnPoint");
            if (spawns != null && spawns.Length > 0)
            {
                GameObject nearest = null;
                float best = float.MaxValue;
                foreach (var s in spawns)
                {
                    float d = (s.transform.position - before).sqrMagnitude;
                    if (d < best) { best = d; nearest = s; }
                }
                if (nearest != null)
                {
                    // temporariamente desactivar CharacterController / Rigidbody para evitar queda imediata
                    var cc = GetComponent<CharacterController>();
                    var rb = GetComponent<Rigidbody>();
                    if (cc != null) cc.enabled = false;
                    if (rb != null) rb.isKinematic = true;

                    transform.position = nearest.transform.position + Vector3.up * 0.1f;
                    Debug.Log($"[PlayerSpawnFixer] Teleported to SpawnPoint '{nearest.name}' at {transform.position} (attempt {i}).");

                    // reactivar depois de um FixedUpdate
                    yield return new WaitForFixedUpdate();
                    if (cc != null) cc.enabled = true;
                    if (rb != null) rb.isKinematic = false;

                    // se estiver ok termina
                    if (transform.position.y > minAcceptY) yield break;
                }
            }

            // Tenta NavMesh sample
            #if UNITY_2018_3_OR_NEWER
            if (NavMesh.SamplePosition(before, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                var cc = GetComponent<CharacterController>();
                var rb = GetComponent<Rigidbody>();
                if (cc != null) cc.enabled = false;
                if (rb != null) rb.isKinematic = true;

                transform.position = hit.position + Vector3.up * 0.1f;
                Debug.Log($"[PlayerSpawnFixer] Teleported to NavMesh at {transform.position} (attempt {i}).");

                yield return new WaitForFixedUpdate();
                if (cc != null) cc.enabled = true;
                if (rb != null) rb.isKinematic = false;

                if (transform.position.y > minAcceptY) yield break;
            }
            #endif

            // fallback: manter X/Z e elevar Y
            {
                var cc = GetComponent<CharacterController>();
                var rb = GetComponent<Rigidbody>();
                if (cc != null) cc.enabled = false;
                if (rb != null) rb.isKinematic = true;

                transform.position = new Vector3(before.x, fallbackY, before.z);
                Debug.Log($"[PlayerSpawnFixer] Fallback teleport to {transform.position} (attempt {i}).");

                yield return new WaitForFixedUpdate();
                if (cc != null) cc.enabled = true;
                if (rb != null) rb.isKinematic = false;

                if (transform.position.y > minAcceptY) yield break;
            }

            // espera um pouco antes da próxima tentativa
            yield return new WaitForSeconds(0.04f);
        }

        Debug.LogWarning($"[PlayerSpawnFixer] Finished attempts; final pos={transform.position}. If still wrong, inspect parent/other scripts.");
    }
}