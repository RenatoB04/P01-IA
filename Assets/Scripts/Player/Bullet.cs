using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BulletProjectile : MonoBehaviour
{
    [Header("Dano")]
    public float damage = 20f;

    [Header("Vida útil")]
    public float lifeTime = 5f;

    [HideInInspector] public int ownerTeam = -1;
    [HideInInspector] public Transform ownerRoot;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision c)
{
    if (ownerRoot && c.transform.root == ownerRoot)
        return;

    var h = c.collider.GetComponentInParent<Health>();
    if (h)
    {
        Vector3 hitPos = c.GetContact(0).point;

        h.TakeDamageFrom(damage, ownerTeam, ownerRoot ? ownerRoot : transform, hitPos);

        // HUD do jogador: dano causado
        if (DamageFeedUI.Instance && ownerTeam == 1)
            DamageFeedUI.Instance.Push(damage, isCrit: false, targetName: h.name);

        CrosshairUI.Instance?.ShowHit();
    }

    Destroy(gameObject);
}
}
