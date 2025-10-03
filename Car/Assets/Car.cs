using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Car : MonoBehaviour
{
    public Rigidbody rigid;
    public WheelCollider wheel1, wheel2, wheel3, wheel4;
    public float drivespeed, steerspeed;
    public float driftSteerMultiplier = 1.1f;
    public float minRearFriction = 0.7f; // Friction at max speed
    public float frontFriction = 1.0f;
    public float maxRearFriction = 1.0f; // Friction at zero speed
    public float maxDriftSpeed = 40f; // Speed at which friction is lowest
    public float driftTurnThreshold = 0.35f;
    public float driftAssist = 100f;
    float horizontalInput, verticalInput;
    public float idleDrag = 2f;
    public float movingDrag = 0.05f;

    // Input System
    private PlayerInput playerInput;
    private InputAction moveAction;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Move"];
        }
    }

    void Update()
    {
        if (moveAction != null)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            horizontalInput = move.x;
            verticalInput = move.y;
        }
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
    }

    void SetWheelFriction(WheelCollider wheel, float friction)
    {
        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = friction;
        wheel.sidewaysFriction = sidewaysFriction;
    }
}