using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Script CENTRAL de Inputs.
/// Todos os outros scripts (Movimento, Arma, Habilidades) devem ler daqui.
/// </summary>
public class GameInput : NetworkBehaviour
{
    // Singleton Local: Permite que qualquer script no TEU player aceda a isto facilmente
    // sem teres de arrastar referências em todo o lado.
    public static GameInput LocalInput { get; private set; }


    [Header("--- Habilidades ---")]
    [SerializeField] private InputActionReference shieldAction; // (Z)
    [SerializeField] private InputActionReference pulseAction;  // (X)


    public override void OnNetworkSpawn()
    {
        // Se não for o meu boneco, desligo este script para não ler inputs errados
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Define este como o Input Local do cliente
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

   
    // Habilidades
    public bool ShieldTriggered() => shieldAction != null && shieldAction.action.WasPressedThisFrame();
    public bool PulseTriggered() => pulseAction != null && pulseAction.action.WasPressedThisFrame();
}