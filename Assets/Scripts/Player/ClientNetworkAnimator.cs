using Unity.Netcode.Components;
using UnityEngine;

namespace InfimaGames.LowPolyShooterPack
{
    [DisallowMultipleComponent]
    public class ClientNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false; 
        }
    }
}