using UnityEngine;

public class Shot : MonoBehaviour
{
    public float speed;
    public float damage = 25f; // Add a damage value

    Rigidbody rb;
    Vector3 velocity;

    void Awake()
    {
        TryGetComponent(out rb);
        if (rb != null)
            rb.useGravity = false; // Disable gravity for the bullet
    }

    void Start()
    {
        velocity = transform.forward * speed;
        Destroy(gameObject, 3f); // Schedule destruction once
    }

    void FixedUpdate()
    {
        if (rb == null) return; // Prevent NullReferenceException
        var displacement = velocity * Time.deltaTime;
        rb.MovePosition(rb.position + displacement);
    }

    void OnCollisionEnter(Collision other)
    {
        // Deal damage if the hit object has CarHealth
        var carHealth = other.collider.GetComponentInParent<CarHealth>();
        if (carHealth != null && !carHealth.IsDestroyed)
        {
            carHealth.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}