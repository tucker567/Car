using UnityEngine;

public class WindParticleController : MonoBehaviour
{
    public ParticleSystem customParticleSystem;
    public Vector3 windDirection = new Vector3(5f, 0f, 0f); // Example: wind blowing right
    public Transform playerTransform; // Assign this in the inspector

    void Start()
    {
        var main = customParticleSystem.main;

        var forceOverLifetime = customParticleSystem.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.x = windDirection.x;
        forceOverLifetime.y = windDirection.y;
        forceOverLifetime.z = windDirection.z;
    }

    void LateUpdate()
    {
        if (playerTransform != null)
        {
            // Move to player's position, keep current rotation
            transform.position = playerTransform.position;
            // Do NOT set transform.rotation, so rotation stays unchanged
        }
    }
}