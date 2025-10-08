using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionWaypointManager : MonoBehaviour
{
    public UnityEngine.Camera mainCamera;
    public Image waypointImage;
    public TextMeshProUGUI meter;
    public Vector3 RectOffset;
    public Transform player; // Assign your car/player transform here
    public Transform[] targets;
    public float switchDistance = 10f; // Distance to switch to next waypoint

    private int currentTargetIndex = 0;

    void Update()
    {
        if (targets == null || targets.Length == 0 || currentTargetIndex >= targets.Length)
        {
            waypointImage.enabled = false;
            meter.enabled = false;
            return;
        }

        Transform target = targets[currentTargetIndex];

        float minX = waypointImage.GetPixelAdjustedRect().width / 2;
        float maxX = Screen.width - minX;

        float minY = waypointImage.GetPixelAdjustedRect().height / 2;
        float maxY = Screen.height - minY;

        Vector3 pos = mainCamera.WorldToScreenPoint(target.position + RectOffset);

        if (Vector3.Dot((target.position - player.position), player.forward) < 0)
        {
            if (pos.x < Screen.width / 2)
                pos.x = maxX;
            else
                pos.x = minX;
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        waypointImage.transform.position = pos;

        float distance = Vector3.Distance(target.position, player.position);
        meter.text = ((int)distance).ToString() + "m";

        // Switch to next waypoint if close enough
        if (distance < switchDistance)
        {
            currentTargetIndex++;
        }
    }
}
