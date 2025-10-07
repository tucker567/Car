using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Car : MonoBehaviour
{
    [Header("Boost/Heat System")]
    public float maxHeat = 100f;
    public float heat = 0f;
    public float heatGainRate = 40f; // per second while boosting
    public float heatCoolRate = 20f; // per second while not boosting
    public float overheatCoolRate = 5f; // per second while overheated
    public float boostForce = 5000f;
    public TMP_Text boostText; // Assign in Inspector
    private bool boosting = false;
    private bool overheated = false;
    public float overheatSlowMultiplier = 0.5f; // Car speed when overheated

    [Header("Car Components")]
    public Rigidbody rigid;
    public WheelCollider wheel1, wheel2, wheel3, wheel4;

    [Header("Driving Settings")]
    public float drivespeed, steerspeed;
    public float idleDrag = 2f; // Linear drag when idle
    public float movingDrag = 0.05f; // Linear drag when moving
    public float centerOfMass = -0.5f;

    [Header("Drift & Friction Settings")]
    public float driftSteerMultiplier = 1.1f; // How much to increase steering when drifting
    public float minRearFriction = 0.7f; // Friction at max speed
    public float frontFriction = 1.0f;
    public float maxRearFriction = 1.0f; // Friction at zero speed
    public float maxDriftSpeed = 40f; // Speed at which friction is lowest
    public float driftTurnThreshold = 0.35f; // Minimum input to consider as turning
    public float driftAssist = 100f;

    [Header("Audio")]
    public AudioSource engine;           // Drag the engine AudioSource here in Inspector
    [Range(0f, 4f)]
    public float engineVolume = 2f;      // Increase max and default value

    // Internal state
    float horizontalInput, verticalInput;

    // Input System
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction unstuckAction;

    void Start()
    {
        rigid.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
            unstuckAction = playerInput.actions.FindAction("Unstuck");
        }
    }

    bool unstuckInProgress = false;

    void Update()
    {

        // Boost input (Left Shift)
        if (Keyboard.current != null)
        {
            // Allow boosting if not overheated and heat is below maxHeat
            boosting = Keyboard.current.leftShiftKey.isPressed && !overheated && heat < maxHeat;
        }

        // Heat logic
        if (boosting)
        {
            heat += heatGainRate * Time.deltaTime;
            if (heat >= maxHeat)
            {
                heat = maxHeat;
                overheated = true;
                boosting = false;
            }
        }
        else if (overheated)
        {
            // While overheated, only cool down (slower)
            if (heat > 0f)
            {
                heat -= overheatCoolRate * Time.deltaTime;
                if (heat < 0f) heat = 0f;
            }
            // Only reset overheated when heat is fully cooled
            if (heat <= 0f)
            {
                overheated = false;
            }
        }
        else
        {
            // Not boosting, not overheated: normal cooldown
            if (heat > 0f)
            {
                heat -= heatCoolRate * Time.deltaTime;
                if (heat < 0f) heat = 0f;
            }
        }

        // Update TMP text
        if (boostText != null)
        {
            if (overheated)
                boostText.text = $"HEAT: {Mathf.FloorToInt(heat)} (OVERHEATED)";
            else
                boostText.text = $"HEAT: {Mathf.FloorToInt(heat)}";
        }

        if (moveAction != null)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            horizontalInput = move.x;
            verticalInput = move.y;
        }
        else
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");
        }

        // Unstuck button: R key or Input System action
        bool unstuckPressed = false;
        if (unstuckAction != null)
        {
            unstuckPressed = unstuckAction.triggered;
        }
        else if (Keyboard.current != null)
        {
            unstuckPressed = Keyboard.current.rKey.wasPressedThisFrame;
        }
        // Only allow unstuck if car is nearly stopped and not already unstucking
        if (unstuckPressed && !unstuckInProgress && rigid.linearVelocity.magnitude < 0.1f)
        {
            StartCoroutine(SmoothUnstuck());
        }

        // Engine sound pitch and volume based on speed
        if (engine != null)
        {
            float speed = rigid.linearVelocity.magnitude;
            engine.pitch = Mathf.Lerp(0.8f, 2.0f, speed / 40f); // Adjust max speed as needed
            engine.volume = engineVolume; // Set volume from slider
            if (!engine.isPlaying)
                engine.Play();
        }
    }

    System.Collections.IEnumerator SmoothUnstuck()
    {
        unstuckInProgress = true;
        float duration = 1.0f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * 1.5f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
        rigid.isKinematic = true;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = endPos;
        transform.rotation = endRot;
        rigid.isKinematic = false;
        unstuckInProgress = false;
    }

    void FixedUpdate()
    {
        // Only allow boost if at least one wheel is grounded
        bool grounded = false;
        WheelHit hit;
        if (wheel1.GetGroundHit(out hit) && hit.collider != null) grounded = true;
        else if (wheel2.GetGroundHit(out hit) && hit.collider != null) grounded = true;
        else if (wheel3.GetGroundHit(out hit) && hit.collider != null) grounded = true;
        else if (wheel4.GetGroundHit(out hit) && hit.collider != null) grounded = true;

        // Apply boost force if boosting, not overheated, and grounded
        if (boosting && grounded)
        {
            rigid.AddForce(transform.forward * boostForce * Time.fixedDeltaTime, ForceMode.Acceleration);
        }
    float speedMultiplier = (overheated ? overheatSlowMultiplier : 1f);
    float motor = verticalInput * drivespeed * speedMultiplier;

        wheel1.motorTorque = motor;
        wheel2.motorTorque = motor;
        wheel3.motorTorque = motor;
        wheel4.motorTorque = motor;

        // Slow down when no input
        if (Mathf.Abs(verticalInput) < 0.01f)
        {
            rigid.linearDamping = idleDrag;
        }
        else
        {
            rigid.linearDamping = movingDrag;
        }

        // Calculate speed-based rear friction
        float speed = rigid.linearVelocity.magnitude;
        float t = Mathf.Clamp01(speed / maxDriftSpeed);
        float rearFriction = Mathf.Lerp(maxRearFriction, minRearFriction, t);

        // Determine if drift assist should be applied (turning)
        bool turning = Mathf.Abs(horizontalInput) > driftTurnThreshold;
        float steer = steerspeed * horizontalInput * (turning ? driftSteerMultiplier : 1f);
        wheel1.steerAngle = steer;
        wheel2.steerAngle = steer;

        // Set friction: front always max, rear scales with speed
        SetWheelFriction(wheel1, frontFriction);
        SetWheelFriction(wheel2, frontFriction);
        SetWheelFriction(wheel3, rearFriction);
        SetWheelFriction(wheel4, rearFriction);

        // Drift assist: add torque to help rotate car into the drift
        if (turning && speed > 1f)
        {
            float driftDirection = Mathf.Sign(horizontalInput);
            rigid.AddTorque(Vector3.up * driftAssist * driftDirection);
        }

        // Auto-roll: gently roll the car if it's significantly tilted
        float uprightDot = Vector3.Dot(transform.up, Vector3.up);
        if (uprightDot < 0.7f) // If more than ~45 degrees from upright
        {
            float rollStrength = 60f; // Lower for more natural, higher for faster
            Vector3 rollAxis = Vector3.Cross(transform.up, Vector3.up).normalized;
            rigid.AddTorque(rollAxis * rollStrength);
        }
        // No timer or clamping, so the car can keep rolling naturally
    }

    void SetWheelFriction(WheelCollider wheel, float friction)
    {
        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = friction;
        wheel.sidewaysFriction = sidewaysFriction;
    }
}