using UnityEngine;
using Unity.Netcode;   

public class SpawnsManager : MonoBehaviour
{
    public static SpawnsManager I;
    public Transform[] points;
    int nextIdx = 0;

    void Awake() => I = this;

    public void GetNext(out Vector3 pos, out Quaternion rot)
    {
        if (points == null || points.Length == 0)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return;
        }

        var t = points[nextIdx % points.Length];
        nextIdx++;
        pos = t.position + Vector3.up * 0.1f;
        rot = t.rotation;
    }

    
    public void Place(NetworkObject playerObj)
    {
        GetNext(out var pos, out var rot);
        playerObj.transform.SetPositionAndRotation(pos, rot);
    }
}
