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

    float currentHealth;
    List<Rigidbody> parts = new List<Rigidbody>();
    bool destroyed = false;

    void Awake()
    {
        currentHealth = maxHealth;
        parts.Clear();
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

        if (wheelCollider1 != null) wheelCollider1.enabled = false;
        if (wheelCollider2 != null) wheelCollider2.enabled = false;
        if (wheelCollider3 != null) wheelCollider3.enabled = false;
        if (wheelCollider4 != null) wheelCollider4.enabled = false;

        // Set BoxCollider size to specific XYZ values
        var boxCol = carRigidbody.GetComponentInChildren<BoxCollider>();
        if (boxCol != null)
        {
            Debug.Log("Changing BoxCollider size!");
            boxCol.center = new Vector3(0f, 0f, 0f); // <-- Set your desired center here
            boxCol.size = new Vector3(1f, 1f, 1f); // <-- Set your desired size here
        }
        else
        {
            Debug.LogWarning("BoxCollider not found on carRigidbody!");
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
