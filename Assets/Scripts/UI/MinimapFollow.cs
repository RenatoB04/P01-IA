using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target (player)")]
    public Transform target;                        

    [Header("Settings")]
    public Vector3 offset = new Vector3(0f, 50f, 0f); 
    public float followSmooth = 10f;                

    [Header("Rotation")]
    public bool lockNorthUp = true;                 
    public float pitchDegrees = 90f;                

    void LateUpdate()
    {
        if (target == null) return;

        
        Vector3 desired = new Vector3(target.position.x, target.position.y + offset.y, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);

        
        float yaw = lockNorthUp ? 0f : target.eulerAngles.y;
        transform.rotation = Quaternion.Euler(pitchDegrees, yaw, 0f);
    }
}
