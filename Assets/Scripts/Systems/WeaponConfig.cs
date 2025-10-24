using UnityEngine;

public class WeaponConfig : MonoBehaviour
{
    [Header("Refs")]
    public Transform firePoint;                 
    public GameObject bulletPrefab;             
    public ParticleSystem muzzleFlashPrefab;
    public AudioClip fireSfx;
    public AudioClip emptyClickSfx;             
    public AudioClip reloadSfx;

    [Header("Stats")]
    public string displayName = "Pistol";       
    public bool automatic = false;              
    public float bulletSpeed = 40f;
    public float fireRate = 0.12f;
    public float maxAimDistance = 200f;

    [Header("Ammo")]
    public int magSize = 12;                    
    public int startingReserve = 48;           
    public float reloadTime = 1.4f;             
}
