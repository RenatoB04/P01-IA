using UnityEngine;
using Unity.Netcode;
using System.Collections; // Adicionado para a Coroutine

public class FirstPersonViewFix : NetworkBehaviour
{
    [Header("Refs")]
    [Tooltip("Root dos braços/arma de 1.ª pessoa (viewmodel).")]
    public GameObject firstPersonRoot;

    [Tooltip("O modelo de 3ª pessoa (ex: BlueSoldier_Male).")]
    public GameObject thirdPersonModel;

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

        // --- CORREÇÃO DO BUG DE SPAWN (Race Condition) ---
        // Espera 0.5 segundos para garantir que o PlayerDeathAndRespawn corre primeiro.
        StartCoroutine(SetupVisibility());
    }

    private IEnumerator SetupVisibility()
    {
        // --- CORREÇÃO DO BUG DE SPAWN ---
        // Espera meio segundo
        yield return new WaitForSeconds(0.5f);
        // --- FIM DA CORREÇÃO ---


        // --- LÓGICA DE VISIBILIDADE ---
        if (IsOwner)
        {
            // É O MEU JOGADOR
            if (mainCamera) mainCamera.enabled = true;
            if (weaponCamera) weaponCamera.enabled = true;
            if (audioListener) audioListener.enabled = true;

            if (firstPersonRoot) firstPersonRoot.SetActive(true);
            if (thirdPersonModel) thirdPersonModel.SetActive(false);
        }
        else
        {
            // É UM JOGADOR REMOTO
            if (mainCamera) mainCamera.enabled = false;
            if (weaponCamera) weaponCamera.enabled = false;
            if (audioListener) audioListener.enabled = false;

            if (firstPersonRoot) firstPersonRoot.SetActive(false);
            if (thirdPersonModel) thirdPersonModel.SetActive(true);
        }
        // --- FIM DA LÓGICA ---

        if (!IsOwner) yield break;

        // --- LÓGICA DAS LAYERS (só corre para o Owner) ---

        if (firstPersonRoot == null || mainCamera == null)
        {
            yield break;
        }

        int fpLayer = LayerMask.NameToLayer(firstPersonLayerName);
        if (fpLayer < 0)
        {
            Debug.LogWarning($"[FirstPersonViewFix] A layer '{firstPersonLayerName}' não existe.");
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
        }
        else
        {
            if (fpLayer >= 0)
            {
                int maskMain = mainCamera.cullingMask;
                maskMain &= ~(1 << fpLayer);
                mainCamera.cullingMask = maskMain;
            }
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