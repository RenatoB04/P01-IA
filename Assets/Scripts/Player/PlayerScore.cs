using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class PlayerScore : NetworkBehaviour
{
    [Header("Config")]
    public int pointsPerKill = 100;

    [Header("Replicado")]
    public NetworkVariable<int> Kills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Score = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnKillsChanged;

    public override void OnNetworkSpawn()
    {
        Kills.OnValueChanged += HandleKillsChanged;
        Score.OnValueChanged += HandleScoreChanged;
    }

    public override void OnNetworkDespawn()
    {
        Kills.OnValueChanged -= HandleKillsChanged;
        Score.OnValueChanged -= HandleScoreChanged;
    }

    void HandleKillsChanged(int prev, int curr) => OnKillsChanged?.Invoke(curr);
    void HandleScoreChanged(int prev, int curr) => OnScoreChanged?.Invoke(curr);

    
    public void AwardKillAndPoints()
    {
        if (!IsServer) return;
        Kills.Value += 1;
        Score.Value += pointsPerKill;
    }
}