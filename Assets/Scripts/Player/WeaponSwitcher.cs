using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Weapons in order: 1 = Rifle, 2 = Pistol, 3 = Knife")]
    public GameObject[] weapons;

    [Header("Input Actions")]
    public InputActionReference weapon1; 
    public InputActionReference weapon2; 
    public InputActionReference weapon3; 

    int currentWeaponIndex = 0;

    void OnEnable()
    {
        if (weapon1 != null) weapon1.action.Enable();
        if (weapon2 != null) weapon2.action.Enable();
        if (weapon3 != null) weapon3.action.Enable();
    }

    void OnDisable()
    {
        if (weapon1 != null) weapon1.action.Disable();
        if (weapon2 != null) weapon2.action.Disable();
        if (weapon3 != null) weapon3.action.Disable();
    }

    void Start()
    {
        SelectWeapon(currentWeaponIndex);
    }

    void Update()
    {
        if (weapon1 != null && weapon1.action.triggered) SelectWeapon(0); 
        if (weapon2 != null && weapon2.action.triggered) SelectWeapon(1);
        if (weapon3 != null && weapon3.action.triggered) SelectWeapon(2); 
    }

    void SelectWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length) return;

        for (int i = 0; i < weapons.Length; i++)
            weapons[i].SetActive(i == index);

        currentWeaponIndex = index;
    }

    public GameObject GetActiveWeapon()
    {
        if (weapons == null || weapons.Length == 0) return null;
        return weapons[currentWeaponIndex];
    }
}
