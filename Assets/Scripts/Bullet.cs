using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BulletProjectile : MonoBehaviour
{
    public float damage = 20f;
    public float lifeTime = 5f;

    [HideInInspector] public int ownerTeam = -1;
    [HideInInspector] public Transform ownerRoot;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision c)
    {
        if (ownerRoot && c.transform.root == ownerRoot) return;

        else
        {
            var h = c.collider.GetComponentInParent<Health>();
            if (h) h.TakeDamage(damage, ownerTeam);
        }

        Destroy(gameObject);
    }
}
