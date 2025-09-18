using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] float lifeTime = 3f;

    void Start() => Destroy(gameObject, lifeTime);

    void OnCollisionEnter(Collision _) => Destroy(gameObject);
}
