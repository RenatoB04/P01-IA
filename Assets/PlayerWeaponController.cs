using Unity.Netcode;
using UnityEngine;
using System;
using InfimaGames.LowPolyShooterPack; // Necessário para a classe Character, Inventory, WeaponBehaviour, etc.

// [RequireComponent(typeof(Health))]
public class PlayerWeaponController : NetworkBehaviour
{
    [Header("Network Prefabs (Ligar no Inspector)")]
    [Tooltip("O prefab da sua bala em rede (com NetworkObject e Projectile.cs).")]
    [SerializeField] 
    private GameObject bulletPrefab;

    [Header("Refs")]
    private Health ownerHealth;
    private Character playerCharacter;

    // --- Inicialização de referências ---
    private void Awake()
    {
        ownerHealth = GetComponent<Health>();
        playerCharacter = GetComponent<Character>();

        if (ownerHealth == null)
            Debug.LogError("PlayerWeaponController: Falta componente Health no root do Player.");
        
        if (playerCharacter == null)
            Debug.LogError("PlayerWeaponController: Falta componente Character no root do Player.");
    }

    /// <summary>
    /// Função pública chamada pelo script Weapon.cs do kit para iniciar o disparo.
    /// </summary>
    /// <param name="direction">Direção do tiro (Raycast do centro da câmara).</param>
    /// <param name="origin">Ponto de origem da bala (Socket da arma).</param>
    /// <param name="speed">Velocidade da bala.</param>
    public void FireExternally(Vector3 direction, Vector3 origin, float speed)
    {
        if (!IsOwner) 
            return;

        // O som + VFX do owner já são feitos no Weapon.Fire (muzzleBehaviour.Effect).
        // Aqui só pedimos ao servidor para tratar da bala e notificar os outros clientes.
        if (ownerHealth == null)
        {
            Debug.LogError("PlayerWeaponController: ownerHealth nulo em FireExternally.");
            return;
        }

        SpawnBulletServerRpc(origin, direction, speed, ownerHealth.team.Value, OwnerClientId);
    }
    
    /// <summary>
    /// Chamado pelo Cliente (Owner) para pedir ao Servidor para spawnar a bala.
    /// </summary>
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
        
        // 1. Enviar RPC para todos os clientes para executarem o efeito de muzzle (som + VFX) nos proxies.
        PlayMuzzleEffectClientRpc();

        // 2. Lógica de Spawn da Bala (Server authoritative).
        var bullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(direction));
        
        if (bullet.TryGetComponent<Projectile>(out var projectileScript))
        {
            // Define a velocidade que será aplicada no OnNetworkSpawn da bala.
            projectileScript.initialVelocity.Value = direction * speed; 
            
            // Define os dados de rede para o Projectile saber quem é o dono e a equipa.
            projectileScript.ownerTeam = shooterTeam;
            projectileScript.ownerClientId = shooterClientId;
        }
        else
        {
            Debug.LogError("[PlayerWeaponController] O prefab da bala não tem o script Projectile.cs.");
        }

        // Spawn na rede (Replica para todos os clientes).
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
    
    /// <summary>
    /// Chamado pelo Servidor para reproduzir o efeito de muzzle (som + VFX) nos Proxies (Clientes Remotos).
    /// O Owner não precisa disto porque já executa o muzzleBehaviour.Effect() localmente no Weapon.Fire().
    /// </summary>
    [ClientRpc]
    private void PlayMuzzleEffectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // O owner já tocou o som e VFX localmente no Weapon.Fire, não repetir.
        if (IsOwner) 
            return; 

        if (playerCharacter == null)
            return;

        var inventory = playerCharacter.GetInventory();
        if (inventory == null)
            return;

        // GetEquipped devolve WeaponBehaviour → fazemos cast explícito para o Weapon do kit.
        var equippedWeapon = inventory.GetEquipped() as InfimaGames.LowPolyShooterPack.Weapon;
        if (equippedWeapon != null)
        {
            // Este método deve chamar muzzleBehaviour.Effect() dentro do Weapon.
            equippedWeapon.PlayMuzzleEffect();
        }
    }
}
