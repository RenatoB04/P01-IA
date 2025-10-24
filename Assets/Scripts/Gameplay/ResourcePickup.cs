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

    void OnTriggerEnter(Collider other)
    {
        // Debug
        // Debug.Log("Pickup Ativado pelo objeto: " + other.gameObject.name);

        // 1. Verificar se é o jogador (pela Tag)
        if (!other.CompareTag(targetTag) && !other.transform.root.CompareTag(targetTag))
        {
            return;
        }

        // --- ENCONTRAR O PLAYER ROOT / COMPONENTES ---
        Transform playerRoot = other.transform.root;
        
        // Ponto A: O Player (CharacterController) pode ter o Health
        Health playerHealth = other.GetComponent<Health>(); 
        Weapon playerWeapon = other.GetComponent<Weapon>();

        // Ponto B: Se não encontrou, procura na RAIZ (o objeto Player principal)
        if (playerHealth == null) playerHealth = playerRoot.GetComponent<Health>();
        if (playerWeapon == null) playerWeapon = playerRoot.GetComponent<Weapon>();
        
        // Ponto C: Procura em todos os filhos da raiz (para WeaponHolder)
        if (playerWeapon == null) playerWeapon = playerRoot.GetComponentInChildren<Weapon>(true);
        // FIM DA PROCURA

        // 2. Tentar aplicar Saúde
        if (healthAmount > 0f)
        {
            if (playerHealth != null)
            {
                playerHealth.Heal(healthAmount);
                // Debug.Log($"Saúde aplicada: {healthAmount}. Novo HP: {playerHealth.currentHealth}"); 
            }
            else
            {
                 Debug.LogWarning("Pickup: Falhou ao encontrar componente Health no Player.");
            }
        }

        // 3. Tentar aplicar Munição
        if (ammoReserveAmount > 0)
        {
            if (playerWeapon != null)
            {
                // ESTA LINHA AGORA EXISTE NO WEAPON.CS
                playerWeapon.AddReserveAmmo(ammoReserveAmount); 
                // Debug.Log($"Munição aplicada: {ammoReserveAmount}.");
            }
            else
            {
                 Debug.LogWarning("Pickup: Falhou ao encontrar componente Weapon no Player.");
            }
        }

        // 4. Destrói o objeto de Pickup se encontrou algum componente para interagir
        if (playerHealth != null || playerWeapon != null)
        {
            // Efeitos e Destruição (restante lógica...)
            if (pickupSound)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            
            if (pickupVFX)
            {
                Instantiate(pickupVFX, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}