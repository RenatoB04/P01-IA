using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    [Header("Recursos")]
    public float healthAmount = 0f;        
    public int ammoReserveAmount = 0;      
    public string targetTag = "Player";    

    [Header("Efeitos")]
    public AudioClip pickupSound;
    public GameObject pickupVFX;

    
    bool pickedUp = false;

    void OnTriggerEnter(Collider other)
    {
        
        if (pickedUp) return;

        
        
        if (!other.CompareTag(targetTag) && !other.transform.root.CompareTag(targetTag))
        {
            return;
        }

        
        pickedUp = true;

        
        Transform playerRoot = other.transform.root;

        
        Health playerHealth = other.GetComponent<Health>();
        Weapon playerWeapon = other.GetComponent<Weapon>();

        
        if (playerHealth == null) playerHealth = playerRoot.GetComponent<Health>();
        if (playerWeapon == null) playerWeapon = playerRoot.GetComponent<Weapon>();

        
        if (playerWeapon == null) playerWeapon = playerRoot.GetComponentInChildren<Weapon>(true);

        
        if (healthAmount > 0f)
        {
            if (playerHealth != null)
            {
                playerHealth.Heal(healthAmount);
            }
            else
            {
                Debug.LogWarning("Pickup: Falhou ao encontrar componente Health no Player.");
            }
        }

        
        if (ammoReserveAmount > 0)
        {
            if (playerWeapon != null)
            {
                
                playerWeapon.AddReserveAmmo(ammoReserveAmount);
            }
            else
            {
                Debug.LogWarning("Pickup: Falhou ao encontrar componente Weapon no Player.");
            }
        }

        
        
        if (playerHealth != null || playerWeapon != null)
        {
            if (pickupSound)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            if (pickupVFX)
            {
                Instantiate(pickupVFX, transform.position, Quaternion.identity);
            }

            
            Collider col = GetComponent<Collider>();
            if (col) col.enabled = false;

            
            Destroy(gameObject);
        }
    }
}