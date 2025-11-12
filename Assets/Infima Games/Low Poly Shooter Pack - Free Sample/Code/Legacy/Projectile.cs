using UnityEngine;
using System.Collections;
using Unity.Netcode; // ADIÇÃO: Necessário para o UNetcode
using InfimaGames.LowPolyShooterPack;
using Random = UnityEngine.Random;

// ALTERAÇÃO: Herda de NetworkBehaviour
public class Projectile : NetworkBehaviour {

    [Range(5, 100)]
    [Tooltip("After how long time should the bullet prefab be destroyed?")]
    public float destroyAfter;
    [Tooltip("If enabled the bullet destroys on impact")]
    public bool destroyOnImpact = false;
    [Tooltip("Minimum time after impact that the bullet is destroyed")]
    public float minDestroyTime;
    [Tooltip("Maximum time after impact that the bullet is destroyed")]
    public float maxDestroyTime;

    // CAMPOS DE REDE (Injetados pelo Servidor/RPC)
    [Header("Network Data")] // ADIÇÃO
    [HideInInspector] public ulong ownerClientId; // Para quem acertar
    [HideInInspector] public int ownerTeam; // ID da equipa do atirador
    public NetworkVariable<Vector3> initialVelocity = new NetworkVariable<Vector3>(Vector3.zero); // Velocidade inicial
    
    // CAMPOS DE IMPACTO DO KIT
    [Header("Impact Effect Prefabs")]
    public Transform [] bloodImpactPrefabs;
    public Transform [] metalImpactPrefabs;
    public Transform [] dirtImpactPrefabs;
    public Transform []    concreteImpactPrefabs;

    private Rigidbody rb;
    private Collider projectileCollider; // Referência ao collider da bala

    // ALTERAÇÃO: Usa OnNetworkSpawn em vez de Start
    public override void OnNetworkSpawn ()
    {
       base.OnNetworkSpawn();

       rb = GetComponent<Rigidbody>();
       projectileCollider = GetComponent<Collider>();

       if (rb == null || projectileCollider == null)
       {
           Debug.LogError("Projectile: Falta Rigidbody ou Collider no prefab da bala.");
           // Usamos NetworkObject.Despawn() se o spawn falhar, se o objeto existir em rede
           if (IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
           return;
       }

       // 1. APLICAR VELOCIDADE: (Apenas o Servidor a define, mas todos a aplicam)
       if (initialVelocity.Value != Vector3.zero)
       {
           rb.linearVelocity = initialVelocity.Value;
       }
       
       // 2. CORREÇÃO DA IGNORÂNCIA DE COLISÃO DO PLAYER (Anti-Self-Hit)
       // Apenas o dono da bala ignora a sua própria colisão
       if (IsOwner) 
       {
           // CORREÇÃO FINAL DA SINTAXE DE BUSCA: Usamos TryGetValue da coleção global
           if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(OwnerClientId, out NetworkObject playerNetworkObject))
           {
               if (playerNetworkObject != null && playerNetworkObject.TryGetComponent<Collider>(out Collider playerCollider))
               {
                   Physics.IgnoreCollision(playerCollider, projectileCollider);
               }
           }
       }
       
       // Start destroy timer
       StartCoroutine (DestroyAfter ());
    }
    
    // If the bullet collides with anything
    private void OnCollisionEnter (Collision collision)
    {
       // LÓGICA CRUCIAL DE REDE: SÓ O SERVIDOR LIDA COM COLISÕES DE BALAS
       if (!IsServer) 
       {
           // O cliente apenas espera que o servidor destrua a bala.
           return;
       }
       
       //Ignore collisions with other projectiles.
       if (collision.gameObject.GetComponent<Projectile>() != null)
          return;
       
       // ADIÇÃO DE LÓGICA DE DANO DE REDE
       if (collision.gameObject.TryGetComponent<Health>(out var healthComponent))
       {
           // Se a bala atingir alguém que não é da nossa equipa (ajuste o team ID conforme a sua lógica)
           if (ownerTeam != -1 && healthComponent.team.Value != ownerTeam)
           {
               // Exemplo: Aplica 20 de dano
               // healthComponent.ApplyDamageServer(20f, ownerTeam, ownerClientId, collision.contacts[0].point, true);
           }
       }
       
       //If destroy on impact is false, start 
       //coroutine with random destroy timer
       if (!destroyOnImpact) 
       {
          StartCoroutine (DestroyTimer ());
       }
       //Otherwise, destroy bullet on impact
       // Usa o Despawn do UNetcode em vez do Destroy (para ser replicado na rede)
       else 
       {
          if (NetworkObject.IsSpawned)
              NetworkObject.Despawn(true); 
          else
              Destroy(gameObject);
          return; // Adicionado return após despawn/destroy
       }

       // --- (O RESTO DO CÓDIGO DE IMPACTO DO KIT PERMANECE IGUAL, mas é executado pelo SERVIDOR) ---
       
       //If bullet collides with "Blood" tag
       if (collision.transform.tag == "Blood") 
       {
          //Instantiate random impact prefab from array
          Instantiate (bloodImpactPrefabs [Random.Range 
             (0, bloodImpactPrefabs.Length)], transform.position, 
             Quaternion.LookRotation (collision.contacts [0].normal));
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }

       //If bullet collides with "Metal" tag
       if (collision.transform.tag == "Metal") 
       {
          //Instantiate random impact prefab from array
          Instantiate (metalImpactPrefabs [Random.Range 
             (0, bloodImpactPrefabs.Length)], transform.position, 
             Quaternion.LookRotation (collision.contacts [0].normal));
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }

       //If bullet collides with "Dirt" tag
       if (collision.transform.tag == "Dirt") 
       {
          //Instantiate random impact prefab from array
          Instantiate (dirtImpactPrefabs [Random.Range 
             (0, bloodImpactPrefabs.Length)], transform.position, 
             Quaternion.LookRotation (collision.contacts [0].normal));
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }

       //If bullet collides with "Concrete" tag
       if (collision.transform.tag == "Concrete") 
       {
          //Instantiate random impact prefab from array
          Instantiate (concreteImpactPrefabs [Random.Range 
             (0, bloodImpactPrefabs.Length)], transform.position, 
             Quaternion.LookRotation (collision.contacts [0].normal));
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }

       //If bullet collides with "Target" tag
       if (collision.transform.tag == "Target") 
       {
          //Toggle "isHit" on target object
          collision.transform.gameObject.GetComponent
             <TargetScript>().isHit = true;
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }
          
       //If bullet collides with "ExplosiveBarrel" tag
       if (collision.transform.tag == "ExplosiveBarrel") 
       {
          //Toggle "explode" on explosive barrel object
          collision.transform.gameObject.GetComponent
             <ExplosiveBarrelScript>().explode = true;
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }

       //If bullet collides with "GasTank" tag
       if (collision.transform.tag == "GasTank") 
       {
          //Toggle "isHit" on gas tank object
          collision.transform.gameObject.GetComponent
             <GasTankScript> ().isHit = true;
          //Destroy bullet object
          if (NetworkObject.IsSpawned) NetworkObject.Despawn(true); else Destroy(gameObject);
          return;
       }
       
       // Se atingiu algo mas não foi destruído acima (fallback):
       if (NetworkObject.IsSpawned)
           NetworkObject.Despawn(true);
       else
           Destroy(gameObject);
    }

    private IEnumerator DestroyTimer () 
    {
       //Wait random time based on min and max values
       yield return new WaitForSeconds
          (Random.Range(minDestroyTime, maxDestroyTime));
       // Destruir na rede
       if (NetworkObject.IsSpawned)
           NetworkObject.Despawn(true);
       else
           Destroy(gameObject);
    }

    private IEnumerator DestroyAfter () 
    {
       //Wait for set amount of time
       yield return new WaitForSeconds (destroyAfter);
       // Destruir na rede
       if (NetworkObject.IsSpawned)
           NetworkObject.Despawn(true);
       else
           Destroy(gameObject);
    }
}