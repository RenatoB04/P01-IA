using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif
#if UNITY_NETCODE_GAMEOBJECTS
using Unity.Netcode;
#endif
using UnityEngine.Rendering;

public class LocalPlayerVisualHider : MonoBehaviour
{
    [Tooltip("Se true, o corpo fica invisível mas continua a projetar sombra.")]
    public bool shadowsOnly = true;

    [Tooltip("Se preenchido, só estes Renderers serão afetados (senão procura todos no filho).")]
    public Renderer[] targetRenderers;

#if PHOTON_UNITY_NETWORKING
    PhotonView pv;
#endif
#if UNITY_NETCODE_GAMEOBJECTS
    NetworkObject no;
#endif

    void Awake()
    {
#if PHOTON_UNITY_NETWORKING
        pv = GetComponent<PhotonView>();
#endif
#if UNITY_NETCODE_GAMEOBJECTS
        no = GetComponent<NetworkObject>();
#endif
    }

    void Start()
    {
        if (!IsLocalOwner()) return;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (var r in targetRenderers)
        {
            if (!r) continue;

            if (shadowsOnly)
            {
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; // aparece sombra mas não a malha
            }
            else
            {
                r.enabled = false; // invisível total
            }
        }
    }

    bool IsLocalOwner()
    {
        // PUN
#if PHOTON_UNITY_NETWORKING
        if (pv) return pv.IsMine;
#endif
        // NGO
#if UNITY_NETCODE_GAMEOBJECTS
        if (no) return no.IsOwner;
#endif
        // single-player / sem rede: assume que este é o local
        return true;
    }
}