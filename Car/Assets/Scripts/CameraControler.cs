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
    public float maxPitch = 80f; // Limit pitch to avoid flipping
    public float minYaw = -60f; // Minimum yaw relative to car
    public float maxYaw = 60f;  // Maximum yaw relative to car
    public float edgeSensitivityMultiplier = 3f; // How much to boost sensitivity at edge

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
            float carYaw = target.eulerAngles.y;
            float relativeYaw = Mathf.DeltaAngle(carYaw, dragYaw);

            // Calculate proximity to edge (0 = center, 1 = at edge)
            float edgeProximity = Mathf.InverseLerp(0, (maxYaw - minYaw) / 2f, Mathf.Min(Mathf.Abs(relativeYaw - minYaw), Mathf.Abs(relativeYaw - maxYaw)));

            // Sensitivity boost only near the edge (e.g., last 20% of range)
            float boostZone = 0.8f; // Start boosting when within 20% of edge
            float edgeBoost = 1f;
            if (edgeProximity > boostZone)
            {
                float boostAmount = (edgeProximity - boostZone) / (1f - boostZone);
                edgeBoost += edgeSensitivityMultiplier * boostAmount;
            }

            // Update relative yaw
            relativeYaw += Mouse.current.delta.x.ReadValue() * mouseSensitivity * edgeBoost * Time.deltaTime;
            relativeYaw = Mathf.Clamp(relativeYaw, minYaw, maxYaw);

            dragYaw = carYaw + relativeYaw;

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
