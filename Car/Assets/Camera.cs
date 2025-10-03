using UnityEngine;

public class Camera : MonoBehaviour
{
    public Transform target; // The object to follow (e.g., your car)
    public Vector3 offset = new Vector3(0, 5, -10); // Offset relative to the car's local space
    public float smoothSpeed = 0.125f; // Smoothing factor for position
    public float orbitSmoothSpeed = 0.07f; // Smoothing factor for orbit angle
    public float orbitLag = 0.5f; // How much the camera lags behind the car's yaw (0 = instant, 1 = max lag)

    private float currentYaw;
    private float targetYaw;

    void LateUpdate()
    {
        if (target == null) return;

        // Get the car's yaw (rotation around Y axis)
        targetYaw = target.eulerAngles.y;
        // Smoothly interpolate the camera's yaw to lag behind the car
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, orbitSmoothSpeed * (1f - orbitLag));

        // Calculate the rotated offset based on the lagged yaw
        Quaternion orbitRotation = Quaternion.Euler(0, currentYaw, 0);
        Vector3 desiredPosition = target.position + orbitRotation * offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Always look at the car's center
        transform.LookAt(target.position);
    }
}
 