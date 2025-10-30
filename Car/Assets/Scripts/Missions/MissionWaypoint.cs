using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionWaypoint : MonoBehaviour
{
    public Image waypointImage;
    public Transform target;
    public TextMeshProUGUI meter;
    public Vector3 RectOffset;

    void Update()
    {
        float minX = waypointImage.GetPixelAdjustedRect().width / 2;
        float maxX = Screen.width - minX;

        float minY = waypointImage.GetPixelAdjustedRect().height / 2;
        float maxY = Screen.height - minY;

        Vector2 pos = UnityEngine.Camera.main.WorldToScreenPoint(target.position + RectOffset);

        if (Vector3.Dot((target.position - transform.position), transform.forward) < 0)
        {
            // Target is behind the camera, clamp to nearest edge
            if (pos.x < Screen.width / 2)
                pos.x = maxX;
            else
                pos.x = minX;
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        waypointImage.transform.position = pos;

        float distance = Vector3.Distance(target.position, transform.position);
        meter.text = ((int)distance).ToString() + "m";
    }
}
