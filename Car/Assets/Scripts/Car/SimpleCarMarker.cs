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

    private bool initialized = false;

    void Awake()
    {
        // Auto-assign components if not set
        if (markerRect == null)
            markerRect = GetComponent<RectTransform>();
        if (markerImage == null)
            markerImage = GetComponent<Image>();
        if (markerOrderText == null)
            markerOrderText = GetComponentInChildren<TMP_Text>();
        Debug.Log($"SimpleCarMarker Awake: markerRect={{markerRect != null}}, markerImage={{markerImage != null}}, markerOrderText={{markerOrderText != null}}");
    }

    public void Initialize(Transform carTarget)
    {
    target = carTarget;
    initialized = true;
    Debug.Log($"SimpleCarMarker initialized with target: {target?.name ?? "NULL"}");
    }

    void Update()
    {
        if (!initialized)
        {
            Debug.LogError($"SimpleCarMarker on {gameObject.name}: Initialize() was never called! Destroying marker.");
            Destroy(gameObject);
            return;
        }
        if (target == null) 
        {
            // Only log this once per second to avoid spam, but be more informative
            if (Time.time % 1.0f < Time.deltaTime)
            {
                Debug.LogWarning($"SimpleCarMarker on {gameObject.name}: target is null! This marker should be destroyed.");
                // Optionally destroy this marker if target is null
                Destroy(gameObject);
            }
            return;
        }

        // Make sure we have a main camera
        if (UnityEngine.Camera.main == null)
        {
            Debug.LogError("SimpleCarMarker: No main camera found!");
            return;
        }

        // Convert world position to screen position
        Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(target.position + offset);

        // Check if target is in front of camera
        bool isInFront = screenPos.z > 0;

        if (markerImage == null)
        {
            Debug.LogError("SimpleCarMarker: markerImage is null!");
            return;
        }

        markerImage.enabled = isInFront;

        if (isInFront)
        {
            // Set marker position
            if (markerRect == null)
            {
                Debug.LogError("SimpleCarMarker: markerRect is null!");
                return;
            }
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