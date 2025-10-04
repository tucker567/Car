using UnityEngine;

public class DriftMarks : MonoBehaviour
{
    public TrailRenderer trailMarks; // Assign 4 TrailRenderers (one for each wheel)
    public WheelCollider wheelColliders; // Assign 4 WheelColliders (same order as trailMarks)
    public GameObject Wheel; // Assign the wheel GameObject
    public float slipThreshold = 0.25f;

    void Update()
    {
        WheelHit hit;
        bool drifting = wheelColliders.GetGroundHit(out hit) && Mathf.Abs(hit.sidewaysSlip) > slipThreshold;
        trailMarks.emitting = drifting;

        // Move The Object this script is attached to up and down with the wheel
        Vector3 wheelPosition = transform.position;
        wheelPosition.y = hit.point.y;
        transform.position = wheelPosition;
    }
}
