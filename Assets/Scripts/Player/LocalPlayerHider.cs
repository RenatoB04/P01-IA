using UnityEngine;
using UnityEngine.Rendering;

public class LocalPlayerVisualHider : MonoBehaviour
{
    [Tooltip("Se true, o corpo fica invisível mas continua a projetar sombra.")]
    public bool shadowsOnly = true;

    [Tooltip("Se preenchido, só estes Renderers serão afetados (senão procura todos nos filhos).")]
    public Renderer[] targetRenderers;

    void Start()
    {
        // Single-player: este player é sempre o local
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (var r in targetRenderers)
        {
            if (!r) continue;

            if (shadowsOnly)
            {
                // Aparece sombra mas não a malha
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            else
            {
                // Invisível total
                r.enabled = false;
            }
        }
    }

    // Se o script for desativado, repõe a visibilidade normal
    void OnDisable()
    {
        if (targetRenderers == null) return;

        foreach (var r in targetRenderers)
        {
            if (!r) continue;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.enabled = true;
        }
    }
}