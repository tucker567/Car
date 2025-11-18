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
    public float stuckCheckInterval = 4f; // seconds to check for stuck (was 2f)
    public float stuckSpeedThreshold = 0.2f; // below this speed is considered stuck (was 0.5f)
    public float unstuckDistanceThreshold = 40f; // far from player (was 30f)
    public float unstuckRotateSpeed = 50f; // degrees per second

    // PID controller variables for speed matching
    private float speedErrorSum = 0f;
    private float lastSpeedError = 0f;
    public float speedKp = 0.5f, speedKi = 0.05f, speedKd = 0.1f;

    [Header("Player Auto-Find")]
    public bool autoFindPlayer = true;
    public string playerTag = "playerCar";
    public float playerSearchInterval = 0.5f;

    private float _nextPlayerSearchTime;
    private bool _playerSearchStarted;

    void Awake()
    {
        if (aiCar == null) aiCar = GetComponent<AiCar>();
    }

    // Update is called once per frame
    void Update()
    {
        // Late-spawned player support
        if ((playerCar == null || (aiCar != null && aiCar.playerCar == null)) && autoFindPlayer)
        {
            if (!_playerSearchStarted)
            {
                _playerSearchStarted = true;
                _nextPlayerSearchTime = Time.time; // search immediately first frame
            }
            if (Time.time >= _nextPlayerSearchTime)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go != null)
                {
                    playerCar = go.transform;
                    if (aiCar != null) aiCar.playerCar = playerCar;
                }
                _nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);
            }
        }

        if (aiCar == null)
        {
            aiCar = GetComponent<AiCar>();
            if (aiCar == null) return; // wait until available
        }

        if (playerCar == null) return; // wait for player to spawn

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

        // --- Collision Avoidance ---
        // Raycast forward, left, and right to avoid obstacles
        float avoidStrength = 0f;
        RaycastHit hit;
        if (Physics.Raycast(aiCar.transform.position + Vector3.up, aiCar.transform.forward, out hit, 6f))
        {
            if (!hit.collider.CompareTag("playerCar") && !hit.collider.CompareTag("AICar"))
            {
                avoidStrength -= 1f;
            }
        }
        if (Physics.Raycast(aiCar.transform.position + Vector3.up, aiCar.transform.right, out hit, 3f))
        {
            if (!hit.collider.CompareTag("playerCar") && !hit.collider.CompareTag("AICar"))
            {
                avoidStrength -= 0.5f;
            }
        }
        if (Physics.Raycast(aiCar.transform.position + Vector3.up, -aiCar.transform.right, out hit, 3f))
        {
            if (!hit.collider.CompareTag("playerCar") && !hit.collider.CompareTag("AICar"))
            {
                avoidStrength += 0.5f;
            }
        }

        // Direction to target
        Vector3 toTarget = targetPos - aiCar.transform.position;
        float distanceToTarget = toTarget.magnitude;
        Vector3 forward = aiCar.transform.forward;
        float angleToTarget = Vector3.Angle(forward, toTarget);
        Vector3 localTarget = aiCar.transform.InverseTransformPoint(targetPos);

        // --- Speed Matching with PID ---
        float speedError = playerSpeed - aiSpeed;
        speedErrorSum += speedError * Time.deltaTime;
        float speedErrorRate = (speedError - lastSpeedError) / Time.deltaTime;
        lastSpeedError = speedError;

        float pidAccel = speedKp * speedError + speedKi * speedErrorSum + speedKd * speedErrorRate;
        pidAccel = Mathf.Clamp(pidAccel, -maxAccel, maxAccel);

        // If REALLY far from player, boost to catch up
        if (distanceToPlayer > 40f)
        {
            aiCar.verticalInput = maxAccel * 2f;
        }
        else if (distanceToTarget < 5f)
        {
            aiCar.verticalInput = pidAccel;
        }
        else
        {
            float forwardDot = Vector3.Dot(forward, toTarget.normalized);
            aiCar.verticalInput = Mathf.Clamp(forwardDot, 0f, maxAccel);
        }

        // --- Slip/Spin Handling ---
        Vector3 localVel = aiCar.transform.InverseTransformDirection(aiCar.rigid.linearVelocity);
        if (Mathf.Abs(localVel.x) > 2f && aiSpeed > 5f)
        {
            // Reduce acceleration and apply stabilizing force
            aiCar.verticalInput *= 0.5f;
            Vector3 driftForce = -aiCar.transform.right * localVel.x * aiCar.driftAssist;
            aiCar.rigid.AddForce(driftForce, ForceMode.Force);
        }

        // --- Steering logic with avoidance ---
        float steerInput = localTarget.x / 5f + avoidStrength;
        steerInput = Mathf.Clamp(steerInput, -maxSteer, maxSteer);

        if (angleToTarget > 135f)
        {
            aiCar.verticalInput = 0.8f;
            aiCar.horizontalInput = steerInput;
        }
        else if (angleToTarget > 90f)
        {
            aiCar.verticalInput = -0.8f;
            aiCar.horizontalInput = steerInput;
        }
        else
        {
            aiCar.horizontalInput = steerInput;
        }
    }
}
