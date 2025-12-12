using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class LossProbe : NetworkBehaviour
{
    public static LossProbe Instance { get; private set; }

    [SerializeField] float interval = 0.5f;   
    [SerializeField] float window = 10f;    

    public float CurrentLossPercent { get; private set; } = -1f;

    ulong _seq = 0;
    readonly Queue<(float time, ulong seq)> sent = new();
    readonly Queue<(float time, ulong seq)> echoed = new();

    float _timer;

    void Awake() => Instance = this;

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening) return;

        
        if (IsServer && !IsClient) { CurrentLossPercent = 0f; return; }
        if (IsServer && IsClient) { CurrentLossPercent = 0f; return; }

        _timer += Time.unscaledDeltaTime;
        if (_timer >= interval)
        {
            _timer = 0f;
            
            _seq++;
            SendProbeServerRpc(_seq);
            sent.Enqueue((Time.unscaledTime, _seq));
        }

        
        float cutoff = Time.unscaledTime - window;
        while (sent.Count > 0 && sent.Peek().time < cutoff) sent.Dequeue();
        while (echoed.Count > 0 && echoed.Peek().time < cutoff) echoed.Dequeue();

        
        if (sent.Count > 0)
        {
            int s = sent.Count;
            int r = echoed.Count;
            int loss = Mathf.Clamp(s - r, 0, s);
            CurrentLossPercent = (loss * 100f) / s;
        }
        else
        {
            CurrentLossPercent = -1f;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendProbeServerRpc(ulong seq, ServerRpcParams rpcParams = default)
    {
        
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
        };
        EchoClientRpc(seq, target);
    }

    [ClientRpc]
    void EchoClientRpc(ulong seq, ClientRpcParams rpcParams = default)
    {
        
        echoed.Enqueue((Time.unscaledTime, seq));
    }
}
