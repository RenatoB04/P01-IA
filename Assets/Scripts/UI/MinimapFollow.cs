using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target (player)")]
    public Transform target;                        // arrasta o Player aqui

    [Header("Settings")]
    public Vector3 offset = new Vector3(0f, 50f, 0f); // altura e posição relativa
    public float followSmooth = 10f;                // velocidade do movimento

    [Header("Rotation")]
    public bool lockNorthUp = true;                 // se false, roda com o player
    public float pitchDegrees = 90f;                // 90 = top-down

    void LateUpdate()
    {
        if (target == null) return;

        // Posição: segue o player no plano XZ
        Vector3 desired = new Vector3(target.position.x, target.position.y + offset.y, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);

        // Rotação: norte fixo ou acompanhar o yaw do player
        float yaw = lockNorthUp ? 0f : target.eulerAngles.y;
        transform.rotation = Quaternion.Euler(pitchDegrees, yaw, 0f);
    }
}
