using UnityEngine;

public class Camera : MonoBehaviour
{
    public Transform target; // The object to follow (e.g., your car)
    public Vector3 offset = new Vector3(0, 5, -10); // Offset relative to the car's local space
    public float smoothSpeed = 0.125f; // Smoothing factor

    void LateUpdate()
    {
        if (target == null) return;
        // Calculate the desired position behind the car using its rotation
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        transform.LookAt(target);
    }
}
