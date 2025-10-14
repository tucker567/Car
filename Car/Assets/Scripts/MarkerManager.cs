using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class MarkerManager : MonoBehaviour
{
    public GameObject markerPrefab; // Assign your marker UI prefab in Inspector
    public Canvas canvas; // Assign your Canvas in Inspector
    public GameObject originalMarker; // Assign your original marker in Inspector

    // Track markers by car transform
    private Dictionary<Transform, GameObject> carMarkers = new Dictionary<Transform, GameObject>();
    private List<Transform> markerOrder = new List<Transform>(); // Oldest to newest
    public IReadOnlyList<Transform> MarkerOrder => markerOrder;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame) // Left click (Input System)
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // Check if the hit object is an AI car (tag or component)
                if (hit.collider.CompareTag("AICar"))
                {
                    Transform car = hit.collider.transform;
                    if (carMarkers.ContainsKey(car))
                    {
                        // Remove marker if it exists
                        Destroy(carMarkers[car]);
                        carMarkers.Remove(car);
                        markerOrder.Remove(car);
                        UpdateMarkerOrders();
                    }
                    else
                    {
                        // Place new marker
                        GameObject markerObj = Instantiate(markerPrefab, canvas.transform);
                        SimpleCarMarker marker = markerObj.GetComponent<SimpleCarMarker>();
                        if (marker == null)
                        {
                            Debug.LogError("SimpleCarMarker script missing on markerPrefab!");
                            return;
                        }
                        marker.target = car;
                        carMarkers[car] = markerObj;
                        markerOrder.Add(car);
                        HideOriginalMarker();
                        UpdateMarkerOrders();
                    }
                }
            }
        }
    }

    void HideOriginalMarker()
    {
        if (originalMarker != null)
        {
            originalMarker.SetActive(false);
        }
    }

    void UpdateMarkerOrders()
    {
        for (int i = 0; i < markerOrder.Count; i++)
        {
            Transform car = markerOrder[i];
            if (carMarkers.TryGetValue(car, out GameObject markerObj))
            {
                SimpleCarMarker marker = markerObj.GetComponent<SimpleCarMarker>();
                if (marker != null)
                {
                    marker.SetOrder(i + 1); // 1-based index
                }
            }
        }
    }
}