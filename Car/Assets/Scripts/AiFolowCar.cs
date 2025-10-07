using UnityEngine;

public class AiFolowCar : MonoBehaviour
{
    public Transform playerCar;
    public AiCar aiCar;
    public float sideOffset = 3f; // Distance to the side of player
    public bool driveOnLeft = true; // Set true for left, false for right
    public float followDistance = 2f; // How far behind/ahead to stay
    public float maxSteer = 1f;
    public float maxAccel = 1f;

    // Update is called once per frame
    void Update()
    {
        if (playerCar == null || aiCar == null) return;

        // Calculate target position next to player
        Vector3 side = driveOnLeft ? -playerCar.right : playerCar.right;
        Vector3 targetPos = playerCar.position + side * sideOffset - playerCar.forward * followDistance;

        // Direction to target
        Vector3 toTarget = targetPos - aiCar.transform.position;
        float distance = toTarget.magnitude;

        // Forward input: accelerate if not close enough
        Vector3 forward = aiCar.transform.forward;
        float forwardDot = Vector3.Dot(forward, toTarget.normalized);
        aiCar.verticalInput = Mathf.Clamp(forwardDot, 0f, maxAccel);

        // Steering input: steer towards target
        Vector3 localTarget = aiCar.transform.InverseTransformPoint(targetPos);
        aiCar.horizontalInput = Mathf.Clamp(localTarget.x / 5f, -maxSteer, maxSteer);
    }
}
