using Unity.Netcode;
using UnityEngine;
using System;
using InfimaGames.LowPolyShooterPack; 


public class PlayerWeaponController : NetworkBehaviour
{
    [Header("Network Prefabs (Ligar no Inspector)")]
    [Tooltip("O prefab da sua bala em rede (com NetworkObject e Projectile.cs).")]
    [SerializeField] 
    private GameObject bulletPrefab;

    [Header("Refs")]
    private Health ownerHealth;
    private Character playerCharacter;

    
    private void Awake()
    {
        ownerHealth = GetComponent<Health>();
        playerCharacter = GetComponent<Character>();

        if (ownerHealth == null)
            Debug.LogError("PlayerWeaponController: Falta componente Health no root do Player.");
        
        if (playerCharacter == null)
            Debug.LogError("PlayerWeaponController: Falta componente Character no root do Player.");
    }

    
    
    
    
    
    
    public void FireExternally(Vector3 direction, Vector3 origin, float speed)
    {
        if (!IsOwner) 
            return;

        
        
        if (ownerHealth == null)
        {
            Debug.LogError("PlayerWeaponController: ownerHealth nulo em FireExternally.");
            return;
        }

        SpawnBulletServerRpc(origin, direction, speed, ownerHealth.team.Value, OwnerClientId);
    }
    
    
    
    
    [ServerRpc]
    private void SpawnBulletServerRpc(
        Vector3 position,
        Vector3 direction,
        float speed,
        int shooterTeam,
        ulong shooterClientId)
    {
        if (!IsServer) 
            return;
        
        if (bulletPrefab == null)
        {
            Debug.LogError("[PlayerWeaponController] Bullet Prefab nulo. Não é possível spawnar.");
            return;
        }
        
        
        PlayMuzzleEffectClientRpc();

        
        var bullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(direction));
        
        if (bullet.TryGetComponent<Projectile>(out var projectileScript))
        {
            
            projectileScript.initialVelocity.Value = direction * speed; 
            
            
            projectileScript.ownerTeam = shooterTeam;
            projectileScript.ownerClientId = shooterClientId;
        }
        else
        {
            Debug.LogError("[PlayerWeaponController] O prefab da bala não tem o script Projectile.cs.");
        }

        
        if (bullet.TryGetComponent<NetworkObject>(out var no))
        {
            try
            {
                no.Spawn(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerWeaponController] Falha ao Spawn do NetworkObject. " +
                               $"Confere se o Bullet Prefab está registado no NetworkManager > Network Prefabs. Ex: {ex.Message}");
                Destroy(bullet);
            }
        }
        else
        {
            Debug.LogError("[PlayerWeaponController] O prefab da bala não tem NetworkObject no ROOT!");
            Destroy(bullet);
        }
    }
    
    
    
    
    
    [ClientRpc]
    private void PlayMuzzleEffectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        
        if (IsOwner) 
            return; 

        if (playerCharacter == null)
            return;

        var inventory = playerCharacter.GetInventory();
        if (inventory == null)
            return;

        
        var equippedWeapon = inventory.GetEquipped() as InfimaGames.LowPolyShooterPack.Weapon;
        if (equippedWeapon != null)
        {
            
            equippedWeapon.PlayMuzzleEffect();
        }
    }
}
