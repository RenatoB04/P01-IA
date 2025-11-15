using UnityEngine;
using Unity.Netcode;

public class FirstPersonViewFix : NetworkBehaviour
{
    [Header("Refs")]
    [Tooltip("Root dos braços/arma de 1.ª pessoa (viewmodel).")]
    public GameObject firstPersonRoot;

    [Tooltip("Câmara principal do jogador (Main Camera).")]
    public Camera mainCamera;

    [Tooltip("Câmara de armas/braços (se o kit usar uma), opcional.")]
    public Camera weaponCamera;

    [Header("Layer do Viewmodel")]
    [Tooltip("Layer dedicada aos braços/arma. Cria em Project Settings → Tags and Layers.")]
    public string firstPersonLayerName = "FirstPerson";

    [Header("Áudio (opcional)")]
    public AudioListener audioListener; // activo só no owner

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Autodetects básicos
        if (mainCamera == null) mainCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);

        // Câmaras só no owner
        if (mainCamera) mainCamera.enabled = IsOwner;
        if (weaponCamera) weaponCamera.enabled = IsOwner;
        if (audioListener) audioListener.enabled = IsOwner;

        // Se não é o dono, não precisamos de mais nada.
        if (!IsOwner) return;

        // Se não houver viewmodel root, não há o que separar.
        if (firstPersonRoot == null || mainCamera == null)
        {
            Debug.LogWarning("[FirstPersonViewFix] Faltam refs (firstPersonRoot/mainCamera).");
            return;
        }

        // Tenta obter a layer
        int fpLayer = LayerMask.NameToLayer(firstPersonLayerName);
        if (fpLayer < 0)
        {
            Debug.LogWarning($"[FirstPersonViewFix] A layer '{firstPersonLayerName}' não existe. " +
                             "Cria-a em Project Settings → Tags and Layers. " +
                             "Vou continuar sem mexer em layers (pode continuar duplicado se tiveres duas câmaras).");
        }
        else
        {
            // Mete os braços/arma todos na layer seleccionada
            SetLayerRecursively(firstPersonRoot, fpLayer);
        }

        // Se existe Weapon Camera, configuramos TWO-CAM setup
        if (weaponCamera != null && fpLayer >= 0)
        {
            // Main Camera exclui a layer FirstPerson
            int maskMain = mainCamera.cullingMask;
            maskMain &= ~(1 << fpLayer);
            mainCamera.cullingMask = maskMain;

            // Weapon Camera só desenha FirstPerson
            weaponCamera.cullingMask = (1 << fpLayer);
            weaponCamera.clearFlags = CameraClearFlags.Depth; // DepthOnly
            weaponCamera.depth = Mathf.Max(mainCamera.depth + 1f, mainCamera.depth + 1f);

            // Nunca ter dois AudioListeners
            var wl = weaponCamera.GetComponent<AudioListener>();
            if (wl) wl.enabled = false;

            // Planos e FOV seguros (opcional)
            weaponCamera.nearClipPlane = 0.01f;
            weaponCamera.farClipPlane = 500f;

            Debug.Log("[FirstPersonViewFix] Configuração TWO-CAM aplicada (Main exclui FirstPerson; WeaponCamera só FirstPerson).");
        }
        else
        {
            // Single camera setup: não excluir a layer para os braços aparecerem
            // (Se tinhas algum hack a pôr ~0, não há problema; aqui não mexemos.)
            Debug.Log("[FirstPersonViewFix] Configuração SINGLE-CAM (sem WeaponCamera).");
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            if (t) SetLayerRecursively(t.gameObject, layer);
    }
}
