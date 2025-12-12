using UnityEngine;
using Unity.Netcode;
using Unity.Collections; 
using Photon.Pun;        

public class PlayerName : NetworkBehaviour
{
    
    
    public NetworkVariable<FixedString32Bytes> netName = new NetworkVariable<FixedString32Bytes>(
        "Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    
    public string Name => netName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            
            string myName = PhotonNetwork.NickName;

            
            if (string.IsNullOrEmpty(myName))
            {
                myName = "Player " + OwnerClientId;
            }

            
            if (myName.Length > 30) myName = myName.Substring(0, 30);

            
            netName.Value = new FixedString32Bytes(myName);
        }
    }

    
    void OnGUI()
    {
        
        
        
        
    }
}