using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // Necessário para FixedString32Bytes
using Photon.Pun;        // Necessário para ler o NickName do Photon

public class PlayerName : NetworkBehaviour
{
    // Variável de rede para guardar o nome (FixedString é obrigatório para strings em Netcode)
    // Permissão: O DONO pode escrever (WritePermission.Owner), todos podem ler.
    public NetworkVariable<FixedString32Bytes> netName = new NetworkVariable<FixedString32Bytes>(
        "Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Propriedade pública para o ScoreboardUI ler facilmente
    public string Name => netName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Se tivermos um nome no Photon, usamos esse
            string myName = PhotonNetwork.NickName;

            // Se estiver vazio (ex: teste direto no editor), inventamos um nome
            if (string.IsNullOrEmpty(myName))
            {
                myName = "Player " + OwnerClientId;
            }

            // Cortamos o nome se for muito grande (limite de 32 bytes)
            if (myName.Length > 30) myName = myName.Substring(0, 30);

            // Escrevemos na variável de rede. O Netcode avisa todos os outros clientes.
            netName.Value = new FixedString32Bytes(myName);
        }
    }

    // Opcional: Podes meter aqui lógica para atualizar um texto por cima da cabeça do boneco
    void OnGUI()
    {
        // (Se quiseres debug rápido, descomenta isto para ver os nomes no ecrã)
        // Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
        // if (screenPos.z > 0)
        //     GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 100, 20), Name);
    }
}