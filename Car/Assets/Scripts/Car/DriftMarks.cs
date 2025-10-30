using UnityEngine;

public class DriftMarks : MonoBehaviour
{
    public TrailRenderer trailMark;
    public WheelCollider wheelCollider;
    public float slipThreshold = 0.25f;
    public bool LeftSide;
    public Color TestGroundSkidMarkColor = Color.black;
    public Color SandSkidMarkColor = Color.yellow;
    public FindLayer findLayer;

    [Range(0f, 1f)]
    public float trailAlpha = 1f;

    public ParticleSystem driftDust;

    void Start()
    {
        if (driftDust != null)
            driftDust.Play();
    }

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

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(colorToUse, 0.0f), new GradientColorKey(colorToUse, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(trailAlpha, 0.0f), new GradientAlphaKey(trailAlpha, 1.0f) }
            );
            trailMark.colorGradient = grad;

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
            Vector3 contactPoint = hit.point + Vector3.up * 0.02f;
            float xOffset = LeftSide ? 0.1f : -0.1f;
            Vector3 offsetPos = contactPoint + transform.right * xOffset;
            trailMark.transform.position = new Vector3(offsetPos.x, offsetPos.y, offsetPos.z);

            // --- Drift Dust Particle System ---
            if (driftDust != null)
            {
                driftDust.transform.position = offsetPos;

                // Emit dust only when drifting
                var emission = driftDust.emission;
                emission.enabled = drifting;
            }
        }
        else
        {
            if (driftDust != null)
            {
                var emission = driftDust.emission;
                emission.enabled = false;
            }
        }
    }
}
