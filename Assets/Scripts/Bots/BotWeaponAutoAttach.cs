using UnityEngine;

/// <summary>
/// Script que automaticamente associa uma arma a um bot.
/// Coloca a arma num ponto definido (weaponHolder) e configura o shootPoint no BotCombat.
/// </summary>
public class BotWeaponAutoAttach : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Onde a arma vai ficar presa (mão, WeaponHolder, etc.).")]
    public Transform weaponHolder; // Transform onde a arma será instanciada

    [Tooltip("Prefab da arma do bot (rifle por defeito).")]
    public GameObject weaponPrefab; // Prefab da arma a instanciar

    [Tooltip("Nome do transform dentro da arma que será usado como ponta do cano (shoot point).")]
    public string muzzleTransformName = "Muzzle"; // Nome do transform que será usado como shootPoint

    [Header("Opções")]
    [Tooltip("Destruir qualquer arma que já esteja como filho do holder.")]
    public bool clearExistingChildren = true; // Remove armas antigas do holder

    private BotCombat combat; // Referência ao script de combate do bot

    void Awake()
    {
        combat = GetComponent<BotCombat>();

        // Verifica se o holder está definido
        if (!weaponHolder)
        {
            Debug.LogWarning($"[BotWeaponAutoAttach] {name}: weaponHolder não está definido.");
            return;
        }

        // Verifica se o prefab está definido
        if (!weaponPrefab)
        {
            Debug.LogWarning($"[BotWeaponAutoAttach] {name}: weaponPrefab não está definido.");
            return;
        }

        // Remove quaisquer armas antigas do holder
        if (clearExistingChildren)
        {
            for (int i = weaponHolder.childCount - 1; i >= 0; i--)
            {
                Destroy(weaponHolder.GetChild(i).gameObject);
            }
        }

        // Instancia a nova arma no holder
        GameObject weaponInstance = Instantiate(weaponPrefab, weaponHolder);
        weaponInstance.transform.localPosition = Vector3.zero;
        weaponInstance.transform.localRotation = Quaternion.identity;
        weaponInstance.transform.localScale = Vector3.one;

        // Configura shootPoint e eyes do BotCombat
        if (combat != null)
        {
            Transform muzzle = null;

            // Procura o transform dentro da arma que corresponde à ponta do cano
            if (!string.IsNullOrEmpty(muzzleTransformName))
            {
                var allChildren = weaponInstance.GetComponentsInChildren<Transform>();
                foreach (var t in allChildren)
                {
                    if (t.name == muzzleTransformName)
                    {
                        muzzle = t;
                        break;
                    }
                }
            }

            // Se não encontrar, usa o transform principal da arma
            if (!muzzle) muzzle = weaponInstance.transform;

            combat.shootPoint = muzzle;

            // Se o eyes não estiver definido, usa o shootPoint
            if (!combat.eyes)
                combat.eyes = muzzle;
        }
    }
}
