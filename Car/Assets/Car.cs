using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Car : MonoBehaviour
{
    public Rigidbody rigid;
    public WheelCollider wheel1, wheel2, wheel3, wheel4;
    public float drivespeed, steerspeed;
    public float driftSteerMultiplier = 1.1f; // How much to increase steering when drifting
    public float minRearFriction = 0.7f; // Friction at max speed
    public float frontFriction = 1.0f;
    public float maxRearFriction = 1.0f; // Friction at zero speed
    public float maxDriftSpeed = 40f; // Speed at which friction is lowest
    public float driftTurnThreshold = 0.35f; // Minimum input to consider as turning
    public float driftAssist = 100f;
    float horizontalInput, verticalInput;
    public float idleDrag = 2f; // Linear drag when idle
    public float movingDrag = 0.05f; // Linear drag when moving

    // Input System
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction unstuckAction;

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
        if (moveAction != null)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            horizontalInput = move.x;
            verticalInput = move.y;
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
        float motor = verticalInput * drivespeed;

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