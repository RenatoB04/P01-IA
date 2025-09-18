using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform firePoint;          // empty na ponta da arma
    [SerializeField] GameObject bulletPrefab;      // prefab do proj�til
    [SerializeField] Transform cam;                // arrasta o cameraRoot (ou deixa vazio p/ usar Camera.main)

    [Header("Input")]
    [SerializeField] InputActionReference shootAction; // Player/Fire

    [Header("Settings")]
    [SerializeField] float bulletSpeed = 40f;
    [SerializeField] float fireRate = 0.12f;
    [SerializeField] float maxAimDistance = 200f;

    float nextFire;
    CharacterController playerCC;

    void Awake()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        playerCC = GetComponentInParent<CharacterController>();
    }

    void OnEnable() { if (shootAction) shootAction.action.Enable(); }
    void OnDisable() { if (shootAction) shootAction.action.Disable(); }

    void Update()
    {
        if (shootAction && shootAction.action.IsPressed() && Time.time >= nextFire)
        {
            Shoot();
            nextFire = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        if (!bulletPrefab || !firePoint) return;

        
        Vector3 dir;
        Ray ray = new Ray(cam ? cam.position : firePoint.position, cam ? cam.forward : firePoint.forward);
        if (Physics.Raycast(ray, out var hit, maxAimDistance, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - firePoint.position).normalized;  
        else
            dir = (ray.GetPoint(maxAimDistance) - firePoint.position).normalized;

        
        var bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));

        
        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.velocity = dir * bulletSpeed;

        
        bullet.transform.position += dir * 0.03f;

        /
        if (muzzleFlash) muzzleFlash.Play();
        if (fireAudio) fireAudio.PlayOneShot(fireAudio.clip);
    }

}
