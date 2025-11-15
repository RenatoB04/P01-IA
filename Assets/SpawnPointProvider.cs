// SpawnPointsProvider.cs
// Coloca este componente numa GameObject da cena (por exemplo "SpawnPointsProvider")
// e arrasta os dois empties (SP_A e SP_B) para os campos "Spawn A" e "Spawn B".

using System;
using UnityEngine;

public class SpawnPointsProvider : MonoBehaviour
{
    public static SpawnPointsProvider Instance { get; private set; }

    [Header("Arrasta aqui os spawn points da cena")]
    [SerializeField] private Transform spawnA;
    [SerializeField] private Transform spawnB;

    [Header("Opcional: auto-descoberta por Tag (se não arrastares)")]
    [SerializeField] private bool autoDiscoverByTag = true;
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Mantém o primeiro provider e evita duplicados.
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Tentativa de auto-descoberta se os campos estiverem vazios.
        if (autoDiscoverByTag && (spawnA == null || spawnB == null) && !string.IsNullOrEmpty(spawnPointTag))
        {
            var objs = GameObject.FindGameObjectsWithTag(spawnPointTag);
            if (objs != null && objs.Length > 0)
            {
                Array.Sort(objs, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                if (objs.Length >= 1 && spawnA == null) spawnA = objs[0].transform;
                if (objs.Length >= 2 && spawnB == null) spawnB = objs[1].transform;
            }
        }
    }

    public bool TryGetSpawnA(out Vector3 pos, out Quaternion rot)
    {
        if (spawnA != null)
        {
            pos = spawnA.position;
            rot = spawnA.rotation;
            return true;
        }
        pos = default;
        rot = Quaternion.identity;
        return false;
    }

    public bool TryGetSpawnB(out Vector3 pos, out Quaternion rot)
    {
        if (spawnB != null)
        {
            pos = spawnB.position;
            rot = spawnB.rotation;
            return true;
        }
        pos = default;
        rot = Quaternion.identity;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (spawnA) Gizmos.DrawWireSphere(spawnA.position, 0.5f);
        Gizmos.color = Color.cyan;
        if (spawnB) Gizmos.DrawWireSphere(spawnB.position, 0.5f);
    }
}
