using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    public Transform player;
    public float height = 60f;

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 pos = player.position;
        pos.y = height;
        transform.position = pos;

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
