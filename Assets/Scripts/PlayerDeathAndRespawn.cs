using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerDeathAndRespawn : MonoBehaviour
{
    [Header("Refs")]
    public Health health;                    // arrasta o Health do player
    public Transform spawnPoint;             // ponto inicial
    public GameObject deathMenu;             // Canvas/Panel que mostra o menu
    public Button respawnButton;             // botão "Respawn" 

    [Header("Controlos a desativar quando morre")]
    [Tooltip("Scripts de movimento/tiro/câmara a desligar quando morre")]
    public Behaviour[] componentsToDisable;

    [Header("Opcional")]
    public bool switchToIgnoreRaycastOnDeath = true;  // evita que bots “vejam” o morto
    public int ignoreRaycastLayer = 2;                // 2 = Ignore Raycast
    private int originalLayer;

    CharacterController cc;
    bool isMenuShown;

    void Awake()
    {
        if (!health) health = GetComponent<Health>();
        cc = GetComponent<CharacterController>();
        originalLayer = gameObject.layer;
    }

    void OnEnable()
    {
        if (health) health.OnDied.AddListener(OnPlayerDied);
        if (respawnButton) respawnButton.onClick.AddListener(OnClickRespawn);
        HideMenu();
    }

    void OnDisable()
    {
        if (health) health.OnDied.RemoveListener(OnPlayerDied);
        if (respawnButton) respawnButton.onClick.RemoveListener(OnClickRespawn);
    }

    void OnPlayerDied()
    {
        // Desligar controlos
        SetControlsEnabled(false);

        // Evitar que bots detetem enquanto morto
        if (switchToIgnoreRaycastOnDeath)
            gameObject.layer = ignoreRaycastLayer;

        // Mostrar menu
        ShowMenu();
    }

    public void OnClickRespawn() => Respawn();

    public void Respawn()
    {
        // 1) Repor vida/estado
        health.ResetFullHealth();

        // 2) Reposicionar no spawn (lidar com CharacterController para não “encravar”)
        if (cc) cc.enabled = false;
        transform.position = spawnPoint ? spawnPoint.position : Vector3.zero;
        transform.rotation = spawnPoint ? spawnPoint.rotation : Quaternion.identity;
        if (cc) cc.enabled = true;

        // 3) Repor layer original (para IA voltar a ver)
        if (switchToIgnoreRaycastOnDeath)
            gameObject.layer = originalLayer;

        // 4) Fechar menu e reativar controlos
        HideMenu();
        SetControlsEnabled(true);
    }

    void SetControlsEnabled(bool enabled)
    {
        if (componentsToDisable != null)
        {
            foreach (var b in componentsToDisable)
                if (b) b.enabled = enabled;
        }

        // Cursor/lock state típico de FPS
        Cursor.visible = !enabled; 
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
        Time.timeScale = 1f; // garante que não ficou pausado
    }

    void ShowMenu()
    {
        if (deathMenu) deathMenu.SetActive(true);
        isMenuShown = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void HideMenu()
    {
        if (deathMenu) deathMenu.SetActive(false);
        isMenuShown = false;
    }
}
