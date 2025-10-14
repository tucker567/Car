using System.Collections.Generic;
using UnityEngine;
using TMPro; // Add this for TextMeshPro

public class CarHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] TMP_Text healthText; // Assign in Inspector
    [SerializeField] Rigidbody carRigidbody; // Assign in Inspector
    [SerializeField] WheelCollider wheelCollider1;
    [SerializeField] WheelCollider wheelCollider2;
    [SerializeField] WheelCollider wheelCollider3;
    [SerializeField] WheelCollider wheelCollider4;
    public bool isPlayerCar = false; // Set this in Inspector if it's the player's car
    public BoxCollider boxCollider; // Assign in Inspector if needed

    float currentHealth;
    List<Rigidbody> parts = new List<Rigidbody>();
    bool destroyed = false;

    void Awake()
    {
        currentHealth = maxHealth;
        parts.Clear();
        // In Awake(), this already finds all child Rigidbodies (including wheels if set up right)
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            if (rb.gameObject != this.gameObject)
            {
                rb.isKinematic = true;
                parts.Add(rb);
            }
        }
        if (isPlayerCar)
            UpdateHealthUI();
        else if (healthText != null)
            healthText.gameObject.SetActive(false); // Disable health text for AI
    }

    public void TakeDamage(float amount)
    {
        if (destroyed) return;
        currentHealth -= amount;
        UpdateHealthUI();
        if (currentHealth <= 0)
            FallApart();
    }

    void FallApart()
    {
        destroyed = true;
        foreach (var rb in parts)
        {
            rb.isKinematic = false;
            rb.transform.SetParent(null);

            var meshCol = rb.GetComponent<MeshCollider>();
            if (meshCol == null)
            {
                meshCol = rb.gameObject.AddComponent<MeshCollider>();
                meshCol.convex = true;
            }
            rb.AddForce(Random.onUnitSphere * 2f, ForceMode.Impulse);
        }

        if (carRigidbody != null)
            carRigidbody.isKinematic = false;

        if (wheelCollider1 != null)
        {
            wheelCollider1.enabled = false;
            wheelCollider1.transform.SetParent(null); // Unparent
        }
        if (wheelCollider2 != null)
        {
            wheelCollider2.enabled = false;
            wheelCollider2.transform.SetParent(null); // Unparent
        }
        if (wheelCollider3 != null)
        {
            wheelCollider3.enabled = false;
            wheelCollider3.transform.SetParent(null); // Unparent
        }
        if (wheelCollider4 != null)
        {
            wheelCollider4.enabled = false;
            wheelCollider4.transform.SetParent(null); // Unparent
        }

        // Set BoxCollider size to specific XYZ values
        if (boxCollider != null)
        {
            Debug.Log("Changing BoxCollider size!");
            boxCollider.center = new Vector3(0f, 0f, 0f); // <-- Set your desired center here
            boxCollider.size = new Vector3(1f, 1f, 1f);   // <-- Set your desired size here
        }
        else
        {
            Debug.LogWarning("BoxCollider not assigned on this car!");
        }

        // Remove marker if present
        var markerManager = Object.FindAnyObjectByType<MarkerManager>();
        if (markerManager != null)
        {
            markerManager.RemoveMarker(transform);
        }

        // Disable AiCar script if present
        var aiCar = GetComponent<AiCar>();
        if (aiCar != null)
        {
            aiCar.enabled = false;
        }
    }

    void UpdateHealthUI()
    {
        if (!isPlayerCar) return; // Only update for player car
        if (healthText != null)
            healthText.text = $"Health: {Mathf.Max(0, Mathf.RoundToInt(currentHealth))}";
    }

    public bool IsDestroyed
    {
        get { return destroyed; }
    }
}
