using UnityEngine;

public class FindLayer : MonoBehaviour
{

    // The current ground layer under the car
    public int CurrentGroundLayer { get; private set; } = -1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // ...existing code...
    }

    // Update is called once per frame
    void Update()
    {
        // Raycast down from the car's position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 5f))
        {
            int layer = hit.collider.gameObject.layer;
            string layerName = LayerMask.LayerToName(layer);
            Debug.Log($"Car is on layer: {layerName} (ID: {layer})");
            CurrentGroundLayer = layer;
        }
        else
        {
            CurrentGroundLayer = -1;
        }
    }
}
