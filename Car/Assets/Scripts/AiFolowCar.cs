using UnityEngine;

public class AiFolowCar : MonoBehaviour
{
    public Transform playerCar;
    public AiCar aiCar;
    public float sideOffset = 3f;
    private bool driveOnLeft = true;
    public float followDistance = 2f;
    public float maxSteer = 1f;
    public float maxAccel = 1f;
    public float minFollowDistance = 1.5f; // Minimum safe distance
    public float maxFollowDistance = 4f;   // Maximum allowed distance
    public float sideSwitchThreshold = 2f; // Only switch if other side is much closer

    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private bool isRotatingToUnstuck = false;
    public float stuckCheckInterval = 2f; // seconds to check for stuck
    public float stuckSpeedThreshold = 0.5f; // below this speed is considered stuck
    public float unstuckDistanceThreshold = 30f; // far from player
    public float unstuckRotateSpeed = 50f; // degrees per second

    // Update is called once per frame
    void Update()
    {
        if (playerCar == null || aiCar == null) return;

        float aiSpeed = aiCar.rigid.linearVelocity.magnitude;
        float distanceToPlayer = Vector3.Distance(aiCar.transform.position, playerCar.position);

        // --- Stuck detection ---
        if (!isRotatingToUnstuck)
        {
            if (aiSpeed < stuckSpeedThreshold && distanceToPlayer > unstuckDistanceThreshold)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > stuckCheckInterval)
                {
                    isRotatingToUnstuck = true;
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            // Rotate car to try to get unstuck
            Vector3 toPlayerDir = (playerCar.position - aiCar.transform.position).normalized;
            float angle = Vector3.Angle(aiCar.transform.forward, toPlayerDir);

            if (angle > 20f)
            {
                aiCar.transform.Rotate(Vector3.up, unstuckRotateSpeed * Time.deltaTime);
            }
            else
            {
                isRotatingToUnstuck = false; // Stop rotating when facing player
            }
            return; // Skip normal AI logic while rotating
        }

        // Predict player movement
        Vector3 playerVelocity = Vector3.zero;
        Rigidbody playerRb = playerCar.GetComponent<Rigidbody>();
        if (playerRb != null)
            playerVelocity = playerRb.linearVelocity;

        float playerSpeed = playerVelocity.magnitude;

        Vector3 predictedPlayerPos = playerCar.position + playerVelocity * 0.5f;

        // Adaptive follow distance
        Vector3 toPlayer = playerCar.position - aiCar.transform.position;
        float currentDist = toPlayer.magnitude;
        float desiredFollow = Mathf.Clamp(currentDist, minFollowDistance, maxFollowDistance);

        // Calculate both possible target positions
        Vector3 leftTarget = predictedPlayerPos - playerCar.right * sideOffset + playerCar.forward * desiredFollow;
        Vector3 rightTarget = predictedPlayerPos + playerCar.right * sideOffset + playerCar.forward * desiredFollow;

        // Choose the side that's closer
        float leftDist = (aiCar.transform.position - leftTarget).sqrMagnitude;
        float rightDist = (aiCar.transform.position - rightTarget).sqrMagnitude;

        // Only switch sides if the other side is significantly closer
        if (driveOnLeft)
        {
            if (rightDist + sideSwitchThreshold < leftDist)
                driveOnLeft = false;
        }
        else
        {
            if (leftDist + sideSwitchThreshold < rightDist)
                driveOnLeft = true;
        }

        Vector3 targetPos = driveOnLeft ? leftTarget : rightTarget;

        // Direction to target
        Vector3 toTarget = targetPos - aiCar.transform.position;
        float distanceToTarget = toTarget.magnitude; // <-- Add this line
        Vector3 forward = aiCar.transform.forward;
        float angleToTarget = Vector3.Angle(forward, toTarget);
        Vector3 localTarget = aiCar.transform.InverseTransformPoint(targetPos);

        // --- Speed matching logic ---
        float speedDiff = playerSpeed - aiSpeed;

        // If very far from player, boost to catch up
        if (distanceToPlayer > 20f)
        {
            aiCar.verticalInput = maxAccel * 2f; // Double acceleration
        }
        else if (distanceToTarget < 5f)
        {
            // Match speed smoothly
            aiCar.verticalInput = Mathf.Clamp(speedDiff * 0.2f, -maxAccel, maxAccel);
        }
        else
        {
            // Normal follow logic
            float forwardDot = Vector3.Dot(forward, toTarget.normalized);
            aiCar.verticalInput = Mathf.Clamp(forwardDot, 0f, maxAccel);
        }

        // Steering logic (same as before)
        if (angleToTarget > 135f)
        {
            aiCar.verticalInput = 0.8f;
            aiCar.horizontalInput = Mathf.Clamp(localTarget.x, -maxSteer, maxSteer);
        }
        else if (angleToTarget > 90f)
        {
            aiCar.verticalInput = -0.8f;
            aiCar.horizontalInput = Mathf.Clamp(localTarget.x, -maxSteer, maxSteer);
        }
        else
        {
            aiCar.horizontalInput = Mathf.Clamp(localTarget.x / 5f, -maxSteer, maxSteer);
        }
    }
}
