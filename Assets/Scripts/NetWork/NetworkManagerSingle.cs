using UnityEngine;
using Unity.Netcode;

public class NetcodeBootstrap : MonoBehaviour
{
    void Awake()
    {
        
        var others = FindObjectsOfType<NetcodeBootstrap>();
        if (others.Length > 1) { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
    }
}