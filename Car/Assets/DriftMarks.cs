using UnityEngine;

public class DriftMarks : MonoBehaviour
{
    public TrailRenderer trailMark; // Assign the TrailRenderer for the tire you want
    public WheelCollider wheelCollider; // Assign the WheelCollider for the same tire
    public float slipThreshold = 0.25f;
    public bool LeftSide; // True if this is the left tire, false for right tire
    public Color TestGroundSkidMarkColor = Color.black; // Color of the skid marks
    public Color SandSkidMarkColor = Color.yellow; // Color of the sand skid marks
    public FindLayer findLayer; // Reference to the FindLayer script

    [Range(0f, 1f)]
    public float trailAlpha = 1f; // Transparency of the trail (0 = fully transparent, 1 = fully opaque)

        void Update()
        {
            WheelHit hit;
            bool grounded = wheelCollider.GetGroundHit(out hit);
            bool drifting = grounded && Mathf.Abs(hit.sidewaysSlip) > slipThreshold;
            trailMark.emitting = drifting;

            // Change skid mark color based on ground layer
            if (findLayer != null)
            {
                Color colorToUse = trailMark.startColor;
                if (findLayer.CurrentGroundLayer == 6)
                {
                    colorToUse = TestGroundSkidMarkColor;
                }
                else if (findLayer.CurrentGroundLayer == 7)
                {
                    colorToUse = SandSkidMarkColor;
                }

                // Set gradient for TrailRenderer with custom alpha
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(colorToUse, 0.0f), new GradientColorKey(colorToUse, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(trailAlpha, 0.0f), new GradientAlphaKey(trailAlpha, 1.0f) }
                );
                trailMark.colorGradient = grad;

                // Also set material color if possible
                if (trailMark.material != null)
                {
                    Color matColor = colorToUse;
                    matColor.a = trailAlpha;
                    trailMark.material.color = matColor;
                }
            }

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
