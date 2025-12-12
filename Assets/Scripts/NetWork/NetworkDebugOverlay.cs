using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; 
#endif

public class NetworkDebugOverlay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;   
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private bool visible = true;
    private float fpsTimer;
    private int frames;
    private float fps;

    void Awake()
    {
        
        if (!debugText) debugText = GetComponent<TextMeshProUGUI>();
        if (!debugText) debugText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void Start()
    {
        
        if (debugText) debugText.enabled = visible;
        ForceRefreshNow();
    }

    void Update()
    {
        
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            pressed = true;
#endif

        if (Input.GetKeyDown(toggleKey))
            pressed = true;

        if (pressed)
        {
            visible = !visible;
            if (debugText) debugText.enabled = visible;
            Debug.Log($"[Overlay] Toggle -> {(visible ? "ON" : "OFF")}");
        }

        if (!visible || !debugText) return;

        
        frames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 1f)
        {
            fps = frames / fpsTimer;
            frames = 0;
            fpsTimer = 0f;
        }

        
        ulong ping = 0;
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsClient)
        {
            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            ping = transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        
        string loss = "-";
        if (LossProbe.Instance)
        {
            float v = LossProbe.Instance.CurrentLossPercent;
            if (v >= 0f) loss = v.ToString("F1") + " %";
        }

        
        debugText.text = $"PING: {ping} ms\nLOSS: {loss}\nFPS: {fps:F0}";
    }

    private void ForceRefreshNow()
    {
        if (!debugText) return;
        debugText.text = "PING: 0 ms\nLOSS: -\nFPS: 0";
    }
}
