using UnityEngine;

/// <summary>
/// Gestor de pontos de spawn para jogadores.
/// Deve existir **apenas um** na cena.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("Singleton")]
    [Tooltip("Instância única na cena (preenchida automaticamente no Awake).")]
    public static SpawnManager I { get; private set; }

    [Header("Spawn Points")]
    [Tooltip("Lista de pontos de spawn. A ordem importa (por ex.: 0 = jogador 1, 1 = jogador 2).")]
    public Transform[] points;

    [Header("Round Robin")]
    [Tooltip("Índice interno usado para RoundRobin (não precisas de mexer).")]
    [SerializeField]
    private int nextIndex = 0;

    private void Awake()
    {
        // Singleton simples: garante que há apenas um SpawnsManager por cena.
        if (I != null && I != this)
        {
            Debug.LogWarning("SpawnsManager: Já existe uma instância na cena. Este será destruído.");
            Destroy(gameObject);
            return;
        }

        I = this;
    }

    /// <summary>
    /// Devolve o próximo ponto de spawn em modo RoundRobin.
    /// É isto que o PlayerDeathAndRespawn chama quando o SelectionMode é RoundRobin.
    /// </summary>
    public void GetNext(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("SpawnsManager.GetNext: Array de pontos vazio. A devolver (0,0,0).");
            return;
        }

        // Garante que o índice está dentro dos limites.
        if (nextIndex < 0 || nextIndex >= points.Length)
            nextIndex = 0;

        Transform t = points[nextIndex];

        if (t == null)
        {
            Debug.LogWarning($"SpawnsManager.GetNext: points[{nextIndex}] é nulo. A devolver (0,0,0).");
            pos = Vector3.zero;
            rot = Quaternion.identity;
        }
        else
        {
            pos = t.position;
            rot = t.rotation;
        }

        // Avança o índice para o próximo spawn.
        nextIndex++;
        if (nextIndex >= points.Length)
            nextIndex = 0;
    }

    // Apenas para ajudar a visualizar na Scene (opcional)
    private void OnDrawGizmosSelected()
    {
        if (points == null || points.Length == 0)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < points.Length; i++)
        {
            Transform t = points[i];
            if (t == null) 
                continue;

            // Esfera no ponto
            Gizmos.DrawSphere(t.position, 0.3f);

            // Linha com o forward
            Gizmos.DrawLine(t.position, t.position + t.forward * 1.5f);

            // Label com o índice (só aparece no SceneView com gizmos)
#if UNITY_EDITOR
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.5f, $"Spawn {i}");
#endif
        }
    }
}
