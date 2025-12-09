using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionWaypoint : MonoBehaviour
{
    public Image waypointImage;
    public Transform target; // World-space target to track
    [Tooltip("Optional tag to find target Transform. If provided and 'target' is null, we'll search by tag.")]
    public string targetTag;
    public TextMeshProUGUI meter;
    public Vector3 RectOffset; // Treated as a world-space offset

    [Header("Projection Context")]
    [Tooltip("Camera used for projecting world to screen. Defaults to Camera.main.")]
    public Camera cam;
    [Tooltip("Canvas containing this waypoint UI. Improves correctness for non-Overlay canvases.")]
    public Canvas canvas;
    [Tooltip("Optional pixel offset applied after screen projection (UI space).")]
    public Vector2 screenOffset;

    [Header("Behind-Target Handling")]
    [Tooltip("If true, waypoints behind the camera are reflected to the screen edges instead of appearing in front.")]
    public bool reflectBehindToScreenEdge = true;

    void Awake()
    {
        // Resolve target via tag if 'target' not assigned
        TryResolveTargetByTag();
    }

    void OnEnable()
    {
        // Re-attempt on enable in case scene order changed
        TryResolveTargetByTag();
    }

    void TryResolveTargetByTag()
    {
        if (target == null && !string.IsNullOrEmpty(targetTag))
        {
            var go = GameObject.FindWithTag(targetTag);
            if (go != null)
                target = go.transform;
        }
    }

    void Update()
    {
        if (waypointImage == null) return;

        // If target still null, try to resolve by tag once more (cheap check)
        if (target == null)
        {
            TryResolveTargetByTag();
            if (target == null) return; // Fallback: no target found, do nothing
        }

        var useCam = cam != null ? cam : Camera.main;
        if (useCam == null) return;

        // Project using viewport for cleaner behind-camera handling
        Vector3 worldPos = target.position + RectOffset;
        Vector3 vp = useCam.WorldToViewportPoint(worldPos);
        bool behind = vp.z < 0f;

        // Determine working screen rect based on canvas render mode
        bool overlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;
        float screenW = overlay ? Screen.width : useCam.pixelWidth;
        float screenH = overlay ? Screen.height : useCam.pixelHeight;

        // If behind and reflection enabled, mirror viewport position so indicator sticks to edge
        if (behind && reflectBehindToScreenEdge)
        {
            vp.x = 1f - vp.x; // mirror horizontally
            vp.y = 1f - vp.y; // mirror vertically
            vp.z = 0.001f;    // treat as in front for subsequent math
        }

        // Convert to screen point after potential reflection
        Vector2 sp = new Vector2(vp.x * screenW, vp.y * screenH);

        // Half-size of the marker in pixels
        float halfW = waypointImage.GetPixelAdjustedRect().width / 2f;
        float halfH = waypointImage.GetPixelAdjustedRect().height / 2f;

        float minX = halfW;
        float maxX = screenW - halfW;
        float minY = halfH;
        float maxY = screenH - halfH;

        Vector2 pos = sp + screenOffset;

        // Clamp inside screen bounds (already mirrored if behind)
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // Place depending on canvas render mode
        RectTransform rt = waypointImage.rectTransform;
        if (overlay)
        {
            // Overlay uses absolute screen pixels
            rt.position = pos;
        }
        else if (canvas != null)
        {
            RectTransform canvasRT = canvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, pos, canvas.worldCamera != null ? canvas.worldCamera : useCam, out Vector2 localPos))
                rt.localPosition = localPos;
        }
        else
        {
            // Fallback to absolute position if canvas unknown
            rt.position = pos;
        }

        // Distance from camera to target is typically expected for waypoints
        float distance = Vector3.Distance(target.position, useCam.transform.position);
        if (meter != null)
            meter.text = ((int)distance).ToString() + "m";
    }
}
