using UnityEngine;
using Unity.Netcode;

public class NetcodeBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Garante Singleton
        var others = FindObjectsOfType<NetcodeBootstrap>();
        if (others.Length > 1) { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
    }
}