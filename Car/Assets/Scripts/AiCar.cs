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

    // Inputs set by AiFolowCar
    [HideInInspector] public float horizontalInput, verticalInput;

    void Start()
    {
        rigid.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void FixedUpdate()
    {
        float motor = verticalInput * drivespeed;
        wheel1.motorTorque = motor;
        wheel2.motorTorque = motor;
        wheel3.motorTorque = motor;
        wheel4.motorTorque = motor;

        rigid.linearDamping = Mathf.Abs(verticalInput) < 0.01f ? idleDrag : movingDrag;

        float steer = steerspeed * horizontalInput;
        wheel1.steerAngle = steer;
        wheel2.steerAngle = steer;
    }
}
