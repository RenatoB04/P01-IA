using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;





public class GameInput : NetworkBehaviour
{
    
    
    public static GameInput LocalInput { get; private set; }


    [Header("--- Habilidades ---")]
    [SerializeField] private InputActionReference shieldAction; 
    [SerializeField] private InputActionReference pulseAction;  


    public override void OnNetworkSpawn()
    {
        
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        
        LocalInput = this;

        EnableAllInputs();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            DisableAllInputs();
        }
    }

    private void EnableAllInputs()
    {
        shieldAction?.action.Enable();
        pulseAction?.action.Enable();
    }

    private void DisableAllInputs()
    {
     
        shieldAction?.action.Disable();
        pulseAction?.action.Disable();
    }

   
    
    public bool ShieldTriggered() => shieldAction != null && shieldAction.action.WasPressedThisFrame();
    public bool PulseTriggered() => pulseAction != null && pulseAction.action.WasPressedThisFrame();
}