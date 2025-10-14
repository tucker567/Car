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
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("AICar"))
                {
                    // Always get the root transform with CarHealth
                    Transform car = hit.collider.GetComponentInParent<CarHealth>()?.transform;
                    if (car == null) return;

                    var carHealth = car.GetComponent<CarHealth>();
                    if (carHealth != null && carHealth.IsDestroyed)
                    {
                        RemoveMarker(car);
                        return;
                    }

                    if (carMarkers.ContainsKey(car))
                    {
                        RemoveMarker(car);
                    }
                    else
                    {
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

        // Remove markers from cars that have died
        for (int i = markerOrder.Count - 1; i >= 0; i--)
        {
            Transform car = markerOrder[i];
            var carHealth = car.GetComponent<CarHealth>();
            if (carHealth != null && carHealth.IsDestroyed)
            {
                RemoveMarker(car);
            }
        }
    }

    void FixedUpdate()
    {
        foreach (var car in markerOrder)
        {
            var carHealth = car.GetComponent<CarHealth>();
            if (carHealth != null && carHealth.IsDestroyed)
                return; // Stop all logic if dead

            // ...rest of AI logic...
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

    public void RemoveMarker(Transform car)
    {
        if (carMarkers.ContainsKey(car))
        {
            Destroy(carMarkers[car]);
            carMarkers.Remove(car);
        }
        markerOrder.Remove(car);
        UpdateMarkerOrders();
    }
}