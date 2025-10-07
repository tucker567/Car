using UnityEngine;
using UnityEngine.InputSystem;

public class Camera : MonoBehaviour
{
    public Transform target; // The object to follow (e.g., your car)
    public Vector3 offset = new Vector3(0, 5, -10); // Offset relative to the car's local space
    public float smoothSpeed = 1f; // Smoothing factor for position
    public float orbitSmoothSpeed = 0.14f; // Smoothing factor for orbit angle
    public float orbitLag = 0.9f; // How much the camera lags behind the car's yaw (0 = instant, 1 = max lag)

    private float currentYaw;
    private float targetYaw;
    private bool isDragging = false;
    private float dragYaw = 0f;
    private float dragPitch = 20f; // Default pitch angle
    public float mouseSensitivity = 3f;
    public float returnToCarSpeed = 2f;
    public float minPitch = 5f;
    public float maxPitch = 80f;

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse drag logic using new Input System
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            dragYaw = currentYaw;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            dragYaw += Mouse.current.delta.x.ReadValue() * mouseSensitivity * Time.deltaTime;
            dragPitch -= Mouse.current.delta.y.ReadValue() * mouseSensitivity * Time.deltaTime;
            dragPitch = Mathf.Clamp(dragPitch, minPitch, maxPitch);
            currentYaw = dragYaw;
        }
        else
        {
            targetYaw = target.eulerAngles.y;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * returnToCarSpeed);
            dragPitch = Mathf.Lerp(dragPitch, 20f, Time.deltaTime * returnToCarSpeed); // Return pitch to default
        }

        // Calculate the rotated offset based on the lagged yaw and pitch
        Quaternion orbitRotation = Quaternion.Euler(dragPitch, currentYaw, 0);
        Vector3 desiredPosition = target.position + orbitRotation * offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Always look at the car's center
        transform.LookAt(target.position);
    }
}
