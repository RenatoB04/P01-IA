using UnityEngine;

public class BotWeaponManager : MonoBehaviour
{
    // Cada slot é uma arma (ex: Rifle, Pistola)
    [System.Serializable]
    public class BotWeaponSlot
    {
        [Header("Objecto da arma")]
        public GameObject weaponObject; // o GameObject da arma (Rifle / Pistol)

        [Header("Script Weapon dessa arma")]
        public Weapon weaponScript;     // o componente Weapon dessa arma
    }

    [Header("Armas (ligar no prefab do bot)")]
    [Tooltip("0 = Rifle, 1 = Pistola (não uses Melee por agora)")]
    public BotWeaponSlot[] weapons;

    // Estado interno
    private int currentWeaponIndex = 0;   // começa na 0 (Rifle)
    private bool isSwitching = false;
    private float nextSwitchTime = 0f;

    [Header("Configuração")]
    public float weaponSwitchCooldown = 1.0f; // tempo mínimo entre trocas

    [Header("Debug")]
    public bool debugLogs = false;

    void Awake()
    {
        // Garantir que só a arma inicial está activa
        SelectWeaponInternal(currentWeaponIndex, true);
    }

    // ======================================================
    //  API pública (usada pelo BotCombat)
    // ======================================================

    // Trocar de arma (Rifle -> Pistola, etc)
    public void SelectWeapon(int index)
    {
        // protecção
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;
        if (index == currentWeaponIndex) return;
        if (isSwitching || Time.time < nextSwitchTime) return;

        if (debugLogs)
            Debug.Log($"[BotWeaponManager] Trocar para arma {index}", this);

        SelectWeaponInternal(index, false);
    }

    // devolve o GameObject da arma actual
    public GameObject GetActiveWeaponObject()
    {
        if (weapons == null || weapons.Length == 0) return null;
        return weapons[currentWeaponIndex].weaponObject;
    }

    // devolve o script Weapon da arma actual
    public Weapon GetActiveWeaponScript()
    {
        if (weapons == null || weapons.Length == 0) return null;
        return weapons[currentWeaponIndex].weaponScript;
    }

    // devolve o índice actual
    public int GetCurrentWeaponIndex()
    {
        return currentWeaponIndex;
    }

    // ======================================================
    //  Interno
    // ======================================================

    private void SelectWeaponInternal(int index, bool forceImmediate)
    {
        isSwitching = true;

        // activa só a arma escolhida
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;
            if (weapons[i].weaponObject == null) continue;

            weapons[i].weaponObject.SetActive(i == index);
        }

        currentWeaponIndex = index;

        // reset de estado na arma activa (cooldowns, reload cancelado, etc)
        Weapon activeWpn = GetActiveWeaponScript();
        if (activeWpn != null)
        {
            activeWpn.ResetWeaponState();
        }

        isSwitching = false;

        // cooldown de troca
        if (!forceImmediate)
        {
            nextSwitchTime = Time.time + weaponSwitchCooldown;
        }
    }
}
