using UnityEngine;





public class BotWeaponAutoAttach : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Onde a arma vai ficar presa (mão, WeaponHolder, etc.).")]
    public Transform weaponHolder;

    [Tooltip("Prefab da arma do bot (rifle por defeito).")]
    public GameObject weaponPrefab;

    [Tooltip("Nome do transform dentro da arma que será usado como ponta do cano (shoot point).")]
    public string muzzleTransformName = "Muzzle";

    [Header("Opções")]
    [Tooltip("Destruir qualquer arma que já esteja como filho do holder.")]
    public bool clearExistingChildren = true;

    BotCombat combat;

    void Awake()
    {
        combat = GetComponent<BotCombat>();

        if (!weaponHolder)
        {
            Debug.LogWarning($"[BotWeaponAutoAttach] {name}: weaponHolder não está definido.");
            return;
        }

        if (!weaponPrefab)
        {
            Debug.LogWarning($"[BotWeaponAutoAttach] {name}: weaponPrefab não está definido.");
            return;
        }

        if (clearExistingChildren)
        {
            for (int i = weaponHolder.childCount - 1; i >= 0; i--)
            {
                Destroy(weaponHolder.GetChild(i).gameObject);
            }
        }

        
        GameObject weaponInstance = Instantiate(weaponPrefab, weaponHolder);
        weaponInstance.transform.localPosition = Vector3.zero;
        weaponInstance.transform.localRotation = Quaternion.identity;
        weaponInstance.transform.localScale = Vector3.one;

        
        if (combat != null)
        {
            Transform muzzle = null;

            
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

            
            if (!muzzle) muzzle = weaponInstance.transform;

            combat.shootPoint = muzzle;
            if (!combat.eyes)
                combat.eyes = muzzle;
        }
    }
}
