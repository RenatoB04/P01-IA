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
    public AudioListener audioListener; 

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        
        if (mainCamera == null) mainCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);

        
        if (mainCamera) mainCamera.enabled = IsOwner;
        if (weaponCamera) weaponCamera.enabled = IsOwner;
        if (audioListener) audioListener.enabled = IsOwner;

        
        if (!IsOwner) return;

        
        if (firstPersonRoot == null || mainCamera == null)
        {
            Debug.LogWarning("[FirstPersonViewFix] Faltam refs (firstPersonRoot/mainCamera).");
            return;
        }

        
        int fpLayer = LayerMask.NameToLayer(firstPersonLayerName);
        if (fpLayer < 0)
        {
            Debug.LogWarning($"[FirstPersonViewFix] A layer '{firstPersonLayerName}' não existe. " +
                             "Cria-a em Project Settings → Tags and Layers. " +
                             "Vou continuar sem mexer em layers (pode continuar duplicado se tiveres duas câmaras).");
        }
        else
        {
            
            SetLayerRecursively(firstPersonRoot, fpLayer);
        }

        
        if (weaponCamera != null && fpLayer >= 0)
        {
            
            int maskMain = mainCamera.cullingMask;
            maskMain &= ~(1 << fpLayer);
            mainCamera.cullingMask = maskMain;

            
            weaponCamera.cullingMask = (1 << fpLayer);
            weaponCamera.clearFlags = CameraClearFlags.Depth; 
            weaponCamera.depth = Mathf.Max(mainCamera.depth + 1f, mainCamera.depth + 1f);

            
            var wl = weaponCamera.GetComponent<AudioListener>();
            if (wl) wl.enabled = false;

            
            weaponCamera.nearClipPlane = 0.01f;
            weaponCamera.farClipPlane = 500f;

            Debug.Log("[FirstPersonViewFix] Configuração TWO-CAM aplicada (Main exclui FirstPerson; WeaponCamera só FirstPerson).");
        }
        else
        {
            
            
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
