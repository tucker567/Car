using System.Collections.Generic;
using UnityEngine;
using TMPro; // Add this for TextMeshPro

public class CarHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [Header("Collision Damage Settings")]
    [SerializeField] float lethalImpactSpeed = 18f; // m/s threshold to trigger explosion/death
    [SerializeField] float playerDamageOnHit = 20f; // damage dealt to player when an AI hits them lethally
    [SerializeField] float minDamageSpeed = 10f; // below this, no damage/explosion
    [SerializeField] TMP_Text healthText; // Assign in Inspector
    [SerializeField] Rigidbody carRigidbody; // Assign in Inspector
    [SerializeField] WheelCollider wheelCollider1;
    [SerializeField] WheelCollider wheelCollider2;
    [SerializeField] WheelCollider wheelCollider3;
    [SerializeField] WheelCollider wheelCollider4;
    public bool isPlayerCar = false; // Set this in Inspector if it's the player's car
    public BoxCollider boxCollider; // Assign in Inspector if needed
    public GameObject endScreenUI; // Assign in Inspector if needed
    [Tooltip("Tag used to find End Screen UI when not assigned")] public string endScreenTag = "EndScreenUI";
    public float UIscreenDelay = 3f; // Delay before showing end screen
    [Tooltip("Tag used to find waypoint/marker UI objects to disable when the player dies. Leave empty to use name fallback.")]
    [SerializeField] string markersTag = ""; // Assign in Inspector; tag-based approach preferred
    public string MarkersName = "WayPoint - Image 1(Clone)"; // Legacy: name-based fallback for marker object
    public List<AudioSource> crashSound = new List<AudioSource>(); // Assign in Inspector for crash sound effect
    public AudioSource deathSound; // Assign in Inspector for death sound effect

    public float currentHealth;
    public GameObject explosionEffect;
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

        // Auto-find healthText only for the player car to avoid AI disabling shared UI
        if (isPlayerCar)
        {
            if (healthText == null)
            {
                healthText = GameObject.Find("Canvas/HealthText")?.GetComponent<TMP_Text>();
                if (healthText == null) healthText = GameObject.Find("HealthText")?.GetComponent<TMP_Text>();
                if (healthText == null)
                {
                    GameObject byTag = null;
                    try { byTag = GameObject.FindGameObjectWithTag("HealthText"); } catch { }
                    if (byTag != null) healthText = byTag.GetComponent<TMP_Text>();
                }
                if (healthText == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
                    foreach (var t in all) { if (t != null && t.name == "HealthText") { healthText = t; break; } }
                }
                if (healthText == null)
                    Debug.LogWarning("[CarHealth] Player healthText not found. Assign, name, or tag it 'HealthText'.");
            }
            UpdateHealthUI(); // Ensures initial text
        }

        // Auto-find endScreenUI only for the player car
        if (isPlayerCar && endScreenUI == null)
        {
            // Prefer finding via tag to avoid name dependencies
            if (!string.IsNullOrEmpty(endScreenTag))
            {
                try
                {
                    var byTag = GameObject.FindGameObjectWithTag(endScreenTag);
                    if (byTag != null) endScreenUI = byTag;
                }
                catch { }
                // If not found, search inactive objects via Resources
                if (endScreenUI == null)
                {
                    var allByTag = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in allByTag)
                    {
                        if (go != null)
                        {
                            bool matches = false;
                            try { matches = go.CompareTag(endScreenTag); } catch { matches = false; }
                            if (matches)
                            {
                                endScreenUI = go;
                                break;
                            }
                        }
                    }
                }
            }
            // Fallbacks to name-based search for backwards compatibility
            if (endScreenUI == null)
            {
                endScreenUI = GameObject.Find("Canvas/EndScreen - Panel");
            }
            if (endScreenUI == null)
            {
                endScreenUI = GameObject.Find("EndScreen - Panel");
            }
            if (endScreenUI == null)
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in all) { if (go != null && go.name == "EndScreen - Panel") { endScreenUI = go; break; } }
            }
            if (endScreenUI == null)
            {
                Debug.LogWarning("[CarHealth] Player endScreenUI not found. Assign it, or tag it '" + endScreenTag + "'.");
            }
            else
            {
                Debug.Log("[CarHealth] Found endScreenUI object '" + endScreenUI.name + "'. ActiveSelf=" + endScreenUI.activeSelf + ", scene=" + (endScreenUI.scene.IsValid() ? endScreenUI.scene.name : "(inactive asset)") );
                endScreenUI.SetActive(false); // Ensure it's hidden at start
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (destroyed) return;
        currentHealth -= amount;
        UpdateHealthUI();
        if (currentHealth <= 0)
            FallApart();

        // Play all crash sound at onceif assigned
        if (crashSound != null)
        {
            foreach (var sound in crashSound)
            {
                if (sound != null)
                {
                    sound.Play();
                }
            }
        }
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

        // Disable AiCar and AiFollowCar script if present
        var aiCar = GetComponent<AiCar>();
        if (aiCar != null)
        {
            aiCar.enabled = false;
        }
        var aiFolowCar = GetComponent<AiFolowCar>();
        if (aiFolowCar != null)
        {
            aiFolowCar.enabled = false;
        }

        // Show end screen after a few seconds if this is the player car
        if (isPlayerCar && endScreenUI != null)
        {
            endScreenUI.SetActive(true);
            var popUpCanvas = GameObject.Find("PopUpUI - Canvas");
            if (popUpCanvas != null) popUpCanvas.SetActive(false);
            var gameplayCanvas = GameObject.Find("GamePlay - Canvas");
            if (gameplayCanvas != null) gameplayCanvas.SetActive(false);

        }

        // Disable waypoint/marker UI using tag (preferred), with name fallback for legacy setups
        if (isPlayerCar)
        {
            bool disabledAnyByTag = false;
            if (!string.IsNullOrEmpty(markersTag))
            {
                GameObject[] taggedMarkers = System.Array.Empty<GameObject>();
                try { taggedMarkers = GameObject.FindGameObjectsWithTag(markersTag); } catch { }
                if (taggedMarkers != null && taggedMarkers.Length > 0)
                {
                    foreach (var m in taggedMarkers)
                    {
                        if (m != null) m.SetActive(false);
                    }
                    disabledAnyByTag = taggedMarkers.Length > 0;
                }

                // Also attempt to catch inactive objects in editor/resources (optional safety)
                if (!disabledAnyByTag)
                {
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    int count = 0;
                    foreach (var go in all)
                    {
                        if (go == null) continue;
                        bool matches = false;
                        try { matches = go.CompareTag(markersTag); } catch { matches = false; }
                        if (matches)
                        {
                            go.SetActive(false);
                            count++;
                        }
                    }
                    disabledAnyByTag = count > 0;
                }
            }

            // Legacy name-based fallback
            if (!disabledAnyByTag && !string.IsNullOrEmpty(MarkersName))
            {
                var waypoint = GameObject.Find(MarkersName);
                if (waypoint != null)
                {
                    waypoint.SetActive(false);
                }
            }
        }

        // Instantiate explosion effect if assigned
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        // Play death sound if assigned
        if (deathSound != null)
        {
            // delay death sound slightly to avoid cut-off
            StartCoroutine(PlayDeathSoundWithDelay());

            IEnumerator<WaitForSeconds> PlayDeathSoundWithDelay()
            {
                yield return new WaitForSeconds(1f);
                deathSound.Play();
            }
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

    public float CurrentHealth
    {
        get { return currentHealth; }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (destroyed) return;

        // Try get the other car's health component
        var otherHealth = collision.collider.GetComponentInParent<CarHealth>();
        if (otherHealth == null) return; // Only care about car-to-car collisions

        // Compute relative impact speed (approximate)
        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < minDamageSpeed) return; // ignore small bumps

        bool thisIsPlayer = isPlayerCar;
        bool otherIsPlayer = otherHealth.isPlayerCar;

        // AI hits Player hard enough -> AI explodes and dies, Player takes damage
        if (!thisIsPlayer && otherIsPlayer && relativeSpeed >= lethalImpactSpeed)
        {
            // This AI dies
            TakeDamage(currentHealth + 999f);

            // Damage the player a bit
            otherHealth.TakeDamage(playerDamageOnHit);
            return;
        }

        // Player hits AI hard enough -> AI explodes and dies, Player takes small damage too
        if (thisIsPlayer && !otherIsPlayer && relativeSpeed >= lethalImpactSpeed)
        {
            otherHealth.TakeDamage(otherHealth.CurrentHealth + 999f);
            TakeDamage(playerDamageOnHit * 0.5f);
            return;
        }

        // AI-to-AI lethal collision -> both explode and die
        if (!thisIsPlayer && !otherIsPlayer && relativeSpeed >= lethalImpactSpeed)
        {
            TakeDamage(currentHealth + 999f);
            otherHealth.TakeDamage(otherHealth.CurrentHealth + 999f);
            return;
        }
    }
    public void HealToFull()
    {
        if (isPlayerCar == false) return;
        if (destroyed) return;
        currentHealth = maxHealth;
        UpdateHealthUI();
    }
}
