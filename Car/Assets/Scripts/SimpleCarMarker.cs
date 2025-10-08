using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimpleCarMarker : MonoBehaviour
{
    public Transform target; // The car to follow
    public RectTransform markerRect; // The UI marker (assign in Inspector)
    public Image markerImage; // Assign in Inspector
    public TMP_Text markerOrderText; // Assign in Inspector

    public Vector3 offset; // Optional offset from car position

    public float minScale = 0.5f; // Smallest scale
    public float maxScale = 1.5f; // Largest scale
    public float minDistance = 10f; // Closest distance for max scale
    public float maxDistance = 200f; // Farthest distance for min scale

    void Update()
    {
        if (target == null) return;

        // Convert world position to screen position
        Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(target.position + offset);

        // Check if target is in front of camera
        bool isInFront = screenPos.z > 0;

        markerImage.enabled = isInFront;

        if (isInFront)
        {
            // Set marker position
            markerRect.position = screenPos;

            // Scale marker based on distance
            float distance = Vector3.Distance(UnityEngine.Camera.main.transform.position, target.position);
            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            float scale = Mathf.Lerp(maxScale, minScale, t);
            markerRect.localScale = Vector3.one * scale;
        }
    }

    public void SetOrder(int order)
    {
        if (markerOrderText != null)
            markerOrderText.text = order.ToString();
    }
}