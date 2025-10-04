using UnityEngine;

public class DriftMarks : MonoBehaviour
{
    public TrailRenderer trailMark; // Assign the TrailRenderer for the tire you want
    public WheelCollider wheelCollider; // Assign the WheelCollider for the same tire
    public float slipThreshold = 0.25f;
    public bool LeftSide; // True if this is the left tire, false for right tire

        void Update()
        {
            WheelHit hit;
            bool grounded = wheelCollider.GetGroundHit(out hit);
            bool drifting = grounded && Mathf.Abs(hit.sidewaysSlip) > slipThreshold;
            trailMark.emitting = drifting;

            // Move the TrailRenderer to the ground contact point if grounded
            if (grounded)
            {
                // Offset a little above ground to avoid z-fighting
                Vector3 contactPoint = hit.point + Vector3.up * 0.02f;
                // Offset x inward under the car depending on side
                float xOffset = LeftSide ? 0.1f : -0.1f;
                Vector3 offsetPos = contactPoint + transform.right * xOffset;
                trailMark.transform.position = new Vector3(offsetPos.x, offsetPos.y, offsetPos.z);
            }
        }
}
