using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MissionWaypoint : MonoBehaviour
{
    public Image waypointImage;
    public TextMeshProUGUI meter;
    public Vector3 RectOffset;
    public List<Transform> targets; // Array of targets
    public float activationDistance = 10f; // Distance to switch to next target
    public Transform carTransform; // Reference to the car

    private int currentTargetIndex = 0;

    void Update()
    {
        if (targets == null || targets.Count == 0 || currentTargetIndex >= targets.Count)
        {
            waypointImage.enabled = false;
            meter.text = "";
            return;
        }

        Transform currentTarget = targets[currentTargetIndex];

        float minX = waypointImage.GetPixelAdjustedRect().width / 2;
        float maxX = Screen.width - minX;

        float minY = waypointImage.GetPixelAdjustedRect().height / 2;
        float maxY = Screen.height - minY;

        Vector2 pos = UnityEngine.Camera.main.WorldToScreenPoint(currentTarget.position + RectOffset);

        Transform camTransform = UnityEngine.Camera.main.transform;

        if (Vector3.Dot((currentTarget.position - camTransform.position), camTransform.forward) < 0)
        {
            if (pos.x < Screen.width / 2)
                pos.x = maxX;
            else
                pos.x = minX;
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        waypointImage.transform.position = pos;

        // Use carTransform for distance
        float distance = Vector3.Distance(currentTarget.position, carTransform.position);
        meter.text = ((int)distance).ToString() + "m";

        // Check if close enough to switch to next target
        if (distance < activationDistance)
        {
            currentTarget.gameObject.SetActive(false);

            currentTargetIndex++;
            if (currentTargetIndex < targets.Count)
            {
                targets[currentTargetIndex].gameObject.SetActive(true);
            }
            else
            {
                waypointImage.enabled = false;
                meter.text = "";
            }
        }
    }
}
