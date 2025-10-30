using UnityEngine;

public class AiCar : MonoBehaviour
{
    [Header("Car Components")]
    public Rigidbody rigid;
    public WheelCollider wheel1, wheel2, wheel3, wheel4;

    [Header("Driving Settings")]
    public float drivespeed = 2000f, steerspeed = 30f;
    public float idleDrag = 2f;
    public float movingDrag = 0.05f;

    [Header("Drift & Stability")]
    public float maxRearFriction = 2.5f;
    public float minRearFriction = 1.0f;
    public float driftSteerReduction = 0.5f; // Reduce steering at high speed
    public float driftAssist = 100f; // Counter force to reduce spinning

    [Header("Braking")]
    public float brakeTorque = 5000f; // Increase for stronger braking

    // Inputs set by AiFolowCar
    [HideInInspector] public float horizontalInput, verticalInput;

    public float lowSpeedTorqueMultiplier = 2.5f; // Increase for more hill climbing power
    public float lowSpeedThreshold = 4f; // Speed below which extra torque is applied

    public Transform playerCar; // Assign this from your AI manager or inspector
    public float flipRecoveryDistance = 40f; // Minimum distance from player to flip

    private float flipTimer = 0f;
    private CarHealth carHealth; // Add this

    void Awake()
    {
        carHealth = GetComponent<CarHealth>(); // Get reference
    }

    void Start()
    {
        rigid.centerOfMass = new Vector3(0, -0.5f, 0);

        // Increase traction for all wheels
        SetWheelTraction(wheel1, 2.5f, 2.5f);
        SetWheelTraction(wheel2, 2.5f, 2.5f);
        SetWheelTraction(wheel3, 2.5f, 2.5f);
        SetWheelTraction(wheel4, 2.5f, 2.5f);
    }

    void SetWheelTraction(WheelCollider wheel, float forwardFriction, float sidewaysFriction)
    {
        WheelFrictionCurve forwardCurve = wheel.forwardFriction;
        forwardCurve.stiffness = forwardFriction;
        wheel.forwardFriction = forwardCurve;

        WheelFrictionCurve sidewaysCurve = wheel.sidewaysFriction;
        sidewaysCurve.stiffness = sidewaysFriction;
        wheel.sidewaysFriction = sidewaysCurve;
    }

    public void ApplyBrakes(bool braking)
    {
        float torque = braking ? brakeTorque : 0f;
        wheel1.brakeTorque = torque;
        wheel2.brakeTorque = torque;
        wheel3.brakeTorque = torque;
        wheel4.brakeTorque = torque;
    }

    void FixedUpdate()
    {
        float speed = rigid.linearVelocity.magnitude;

        // Motor torque logic
        float motor = verticalInput * drivespeed;
        if (speed < lowSpeedThreshold && verticalInput > 0.1f)
        {
            motor *= lowSpeedTorqueMultiplier;
        }

        wheel1.motorTorque = motor;
        wheel2.motorTorque = motor;
        wheel3.motorTorque = motor;
        wheel4.motorTorque = motor;

        // Adjust rear wheel friction based on speed
        float rearFriction = Mathf.Lerp(maxRearFriction, minRearFriction, speed / 40f);
        SetWheelTraction(wheel3, rearFriction, rearFriction);
        SetWheelTraction(wheel4, rearFriction, rearFriction);

        // Reduce steering at high speed
        float steerMultiplier = Mathf.Lerp(1f, driftSteerReduction, speed / 40f);
        float steer = steerspeed * horizontalInput * steerMultiplier;
        wheel1.steerAngle = steer;
        wheel2.steerAngle = steer;

        rigid.linearDamping = Mathf.Abs(verticalInput) < 0.01f ? idleDrag : movingDrag;

        // Drift assist: apply counter force if car is sliding
        Vector3 localVel = transform.InverseTransformDirection(rigid.linearVelocity);
        if (Mathf.Abs(localVel.x) > 0.5f && speed > 5f)
        {
            Vector3 driftForce = -transform.right * localVel.x * driftAssist;
            rigid.AddForce(driftForce, ForceMode.Force);
        }

        // --- Self-correction if facing away from target ---
        if (horizontalInput != 0 || verticalInput != 0)
        {
            // Find direction to target (from AiFolowCar)
            Vector3 targetDir = rigid.transform.forward * verticalInput + rigid.transform.right * horizontalInput;
            float facingDot = Vector3.Dot(rigid.transform.forward, targetDir.normalized);

            // If facing away from target, apply extra steering
            if (facingDot < 0)
            {
                float correctionSteer = steerspeed * Mathf.Sign(horizontalInput) * 1.5f; // Stronger steer
                wheel1.steerAngle = correctionSteer;
                wheel2.steerAngle = correctionSteer;

                // Optionally, apply a small torque to help rotate
                rigid.AddTorque(Vector3.up * Mathf.Sign(horizontalInput) * 500f);
            }
        }

        // Example: Apply brakes if not accelerating
        bool isBraking = verticalInput < 0.1f;
        ApplyBrakes(isBraking);

        // --- Flip Recovery System ---
        // Only flip if upside down AND player is far away
        if (Vector3.Dot(transform.up, Vector3.up) < 0.3f)
        {
            flipTimer += Time.fixedDeltaTime;
            float playerDist = playerCar != null ? Vector3.Distance(transform.position, playerCar.position) : Mathf.Infinity;
            if (flipTimer > 1f && playerDist > flipRecoveryDistance)
            {
                // Upright the car
                Vector3 pos = transform.position;
                Quaternion upright = Quaternion.LookRotation(transform.forward, Vector3.up);
                rigid.MovePosition(pos + Vector3.up * 0.5f); // Lift slightly to avoid ground collision
                rigid.MoveRotation(upright);
                rigid.linearVelocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
                flipTimer = 0f;
            }
        }
        else
        {
            flipTimer = 0f;
        }
    }
}
