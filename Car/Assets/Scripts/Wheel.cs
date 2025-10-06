using UnityEngine;

public class Wheel : MonoBehaviour
{
    public WheelCollider wheelCollider;
    public Transform wheelMesh;
    public bool wheelTurn;

    private float currentSteerAngle = 0f;
    public float steerSmoothSpeed = 8f;

    void LateUpdate()
    {
        // Get suspension position and rotation
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelMesh.position = pos;
        wheelMesh.rotation = rot;

        // For front wheels, smoothly interpolate visual steering
        if (wheelTurn == true)
        {
            float targetSteer = wheelCollider.steerAngle;
            currentSteerAngle = Mathf.LerpAngle(currentSteerAngle, targetSteer, steerSmoothSpeed * Time.deltaTime);
            Vector3 euler = wheelMesh.localEulerAngles;
            euler.y = currentSteerAngle;
            wheelMesh.localEulerAngles = euler;
        }
    }
}
