using TMPro;
using UnityEngine;

public class AmmoUI : MonoBehaviour
{
    public static AmmoUI Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public TextMeshProUGUI ammoText;

    public void Set(int inMag, int reserve)
    {
        if (!ammoText) return;
        ammoText.text = $"{inMag}/{reserve}";
    }

    public void Set(string _weaponName, int inMag, int reserve)
    {
        Set(inMag, reserve);
    }

    public void Clear()
    {
        if (ammoText) ammoText.text = "";
    }
}