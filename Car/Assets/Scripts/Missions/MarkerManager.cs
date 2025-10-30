using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class MarkerManager : MonoBehaviour
{
    public GameObject markerPrefab; // Assign your marker UI prefab in Inspector
    public Canvas canvas; // Assign your Canvas in Inspector

    // Track markers by car transform
    private Dictionary<Transform, GameObject> carMarkers = new Dictionary<Transform, GameObject>();
    private List<Transform> markerOrder = new List<Transform>(); // Oldest to newest
    public IReadOnlyList<Transform> MarkerOrder => markerOrder;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Mouse clicked, attempting to cast ray...");
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log($"Raycast hit: {hit.collider.name}, Tag: {hit.collider.tag}");
                
                // Try to find CarHealth component regardless of tag
                Transform car = hit.collider.GetComponentInParent<CarHealth>()?.transform;
                if (car != null)
                {
                    Debug.Log($"Found car with CarHealth: {car.name}");
                    var carHealth = car.GetComponent<CarHealth>();
                    
                    // Only proceed if it's not the player car
                    if (carHealth != null && !carHealth.isPlayerCar)
                    {
                        if (carHealth.IsDestroyed)
                        {
                            Debug.Log("Car is already destroyed, removing marker");
                            RemoveMarker(car);
                            return;
                        }

                        if (carMarkers.ContainsKey(car))
                        {
                            Debug.Log("Removing existing marker for car: " + car.name);
                            RemoveMarker(car);
                        }
                        else
                        {
                            Debug.Log("Creating new marker for car: " + car.name);
                            if (markerPrefab == null)
                            {
                                Debug.LogError("MarkerPrefab is not assigned in MarkerManager!");
                                return;
                            }
                            if (canvas == null)
                            {
                                Debug.LogError("Canvas is not assigned in MarkerManager!");
                                return;
                            }
                            GameObject markerObj = Instantiate(markerPrefab, canvas.transform);
                            SimpleCarMarker marker = markerObj.GetComponent<SimpleCarMarker>();
                            if (marker == null)
                            {
                                Debug.LogError("SimpleCarMarker script missing on markerPrefab!");
                                return;
                            }
                            
                            // Use Initialize method to set target immediately
                            marker.Initialize(car);
                            Debug.Log($"Marker initialized for {car.name}. Target is now: {marker.target?.name ?? "NULL"}");
                            carMarkers[car] = markerObj;
                            markerOrder.Add(car);
                            UpdateMarkerOrders();
                            Debug.Log($"Successfully created marker for {car.name}. Total markers: {carMarkers.Count}");
                        }
                    }
                    else if (carHealth != null && carHealth.isPlayerCar)
                    {
                        Debug.Log("Cannot mark player car");
                    }
                }
                else
                {
                    Debug.Log($"Hit object does not have CarHealth component. Name: {hit.collider.name}");
                }
            }
            else
            {
                Debug.Log("Raycast did not hit anything");
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