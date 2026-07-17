using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 25f;

    [Header("Impact")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private float bulletHoleSize = 0.1f;
    [SerializeField] private float bulletHoleLifetime = 10f;

    public float Damage => damage;

    private void OnCollisionEnter(Collision collision)
    {
        CreateBulletHole(collision);
        collision.gameObject.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        if (collision.gameObject.CompareTag("Target"))
        {
            Debug.Log("Hit " + collision.gameObject.name);
        }

        if (collision.gameObject.CompareTag("Target") || collision.gameObject.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    private void CreateBulletHole(Collision collision)
    {
        if (bulletHolePrefab == null || collision.contactCount == 0)
        {
            return;
        }

        ContactPoint contact = collision.contacts[0];
        Quaternion hitRotation = Quaternion.LookRotation(contact.normal);
        GameObject bulletHole = Instantiate(
            bulletHolePrefab,
            contact.point + contact.normal * 0.001f,
            hitRotation);

        float randomSize = bulletHoleSize * Random.Range(0.8f, 1.2f);
        bulletHole.transform.localScale = Vector3.one * randomSize;
        bulletHole.transform.Rotate(contact.normal, Random.Range(0f, 360f), Space.World);
        bulletHole.transform.SetParent(collision.transform);

        Destroy(bulletHole, bulletHoleLifetime);
    }
}
