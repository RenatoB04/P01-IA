using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform firePoint;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] Transform cam;
    [SerializeField] ParticleSystem muzzleFlash;   // prefab do efeito visual
    [SerializeField] AudioSource fireAudio;        // som do disparo

    [Header("Input")]
    [SerializeField] InputActionReference shootAction;

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

    void OnEnable()
    {
        if (shootAction) shootAction.action.Enable();
    }

    void OnDisable()
    {
        if (shootAction) shootAction.action.Disable();
    }

    void Update()
    {
        if (shootAction && shootAction.action.WasPressedThisFrame())
        {
            if (Time.time >= nextFire)
            {
                Shoot();
                nextFire = Time.time + fireRate;
            }
        }
    }

    void Shoot()
    {
        if (!bulletPrefab || !firePoint) return;

        // dire��o do disparo
        Vector3 dir;
        Ray ray = new Ray(cam ? cam.position : firePoint.position, cam ? cam.forward : firePoint.forward);
        if (Physics.Raycast(ray, out var hit, maxAimDistance, ~0, QueryTriggerInteraction.Ignore))
            dir = (hit.point - firePoint.position).normalized;
        else
            dir = (ray.GetPoint(maxAimDistance) - firePoint.position).normalized;

        // criar bala
        var bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * bulletSpeed;

        // afastar um pouco da arma para n�o colidir logo
        bullet.transform.position += dir * 0.2f;

        // flash
        if (muzzleFlash)
        {
            var flash = Instantiate(muzzleFlash, firePoint.position, firePoint.rotation, firePoint);
            flash.Play();
            Destroy(flash.gameObject, 0.2f);
        }

        // som
        if (fireAudio && fireAudio.clip) fireAudio.PlayOneShot(fireAudio.clip);
    }
}
