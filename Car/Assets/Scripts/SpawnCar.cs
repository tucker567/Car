using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SpawnCar : MonoBehaviour
{
    [System.Serializable]
    public class CarEntry
    {
        public string displayName;
        public GameObject prefab;
    }

    [Header("Car Selection")]
    public List<CarEntry> cars = new List<CarEntry>();
    public int selectedIndex = 0;

    [Header("UI")]
    public TMP_Text carNameText;          // Auto-found if left null
    public string carNameObjectName = "CarNameText"; // Name or Canvas/CarNameText path

    [Header("Spawn Settings")]
    public Camera mainCamera;

    public GameObject Titlesceane;

    [Header("Bunker Entrance Placement")]
    public bool spawnNearBunker = true; // If true, override spawnPoint with random spot near bunkerEntrance
    public Transform bunkerEntrance;    // Assign the bunker entrance transform here
    [Tooltip("Minimum horizontal distance from bunker entrance.")] public float bunkerMinDistance = 3f;
    [Tooltip("Maximum horizontal distance from bunker entrance.")] public float bunkerMaxDistance = 12f;
    [Tooltip("Restrict spawning to this forward arc (degrees) relative to bunker forward. 360 = full circle.")] [Range(0f,360f)] public float bunkerForwardArc = 160f;
    [Tooltip("Extra vertical offset after sampling ground height.")] public float bunkerHeightOffset = 0.2f;
    [Tooltip("Layer mask for ground raycast.")] public LayerMask groundRaycastMask = ~0;
    [Tooltip("Align car 'up' to ground normal if raycast hits.")] public bool alignToGroundNormal = true;
    [Tooltip("Auto-find bunker entrance by name or tag if not assigned.")] public string bunkerAutoFindNamePrefix = "BunkerEntrance_";
    [Tooltip("Tag to search if name search fails (optional).") ] public string bunkerAutoFindTag = "";

    // Runtime reference to the spawned car
    private GameObject spawnedCar;

    [Header("World Generation Integration")]
    public bool autoSpawnOnWorldReady = true; // Auto call Play() when world generation completes

    void Awake()
    {
        // Auto-find bunker entrace if needed
        AutoFindBunkerEntrance();

        AutoFindCarNameText();
        AutoFindBunkerEntrance();
        ClampSelectedIndex();
        UpdateCarNameUI();
    }

    void Start()
    {
        // Wait for world generation to complete before relocating near bunker
        if (spawnNearBunker)
        {
            StartCoroutine(RelocateAfterWorldGeneration());
        }

        // Subscribe to world generation completion to auto-spawn car
        if (autoSpawnOnWorldReady)
        {
            var wg = FindObjectOfType<WorldGenerator>();
            if (wg != null)
            {
                wg.OnGenerationComplete += OnWorldGenerationComplete;
            }
        }
    }

    void AutoFindCarNameText()
    {
        if (carNameText != null) return;

        // Try path then root name
        carNameText = GameObject.Find($"Canvas/{carNameObjectName}")?.GetComponent<TMP_Text>();
        if (carNameText == null)
            carNameText = GameObject.Find(carNameObjectName)?.GetComponent<TMP_Text>();

        // Tag lookup
        if (carNameText == null)
        {
            GameObject byTag = null;
            try { byTag = GameObject.FindGameObjectWithTag(carNameObjectName); } catch { }
            if (byTag != null) carNameText = byTag.GetComponent<TMP_Text>();
        }

        // Exact-name scan
        if (carNameText == null)
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
            foreach (var t in all)
            {
                if (t != null && t.name == carNameObjectName)
                {
                    carNameText = t;
                    break;
                }
            }
        }

        if (carNameText == null)
            Debug.LogWarning($"[SpawnCar] Car name text '{carNameObjectName}' not found. Assign manually or rename/tag it.");
    }

    void ClampSelectedIndex()
    {
        if (cars.Count == 0) { selectedIndex = 0; return; }
        if (selectedIndex < 0) selectedIndex = 0;
        if (selectedIndex >= cars.Count) selectedIndex = cars.Count - 1;
    }

    void AutoFindBunkerEntrance()
    {
        if (bunkerEntrance != null) return;
        // Try exact name prefix scan among scene objects
        var allRoots = gameObject.scene.GetRootGameObjects();
        foreach (var root in allRoots)
        {
            var tList = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in tList)
            {
                if (t != null && t.name.StartsWith(bunkerAutoFindNamePrefix, System.StringComparison.Ordinal))
                {
                    bunkerEntrance = t;
                    return;
                }
            }
        }
        // Tag search if provided
        if (bunkerEntrance == null && !string.IsNullOrEmpty(bunkerAutoFindTag))
        {
            try
            {
                var tagged = GameObject.FindGameObjectWithTag(bunkerAutoFindTag);
                if (tagged != null) bunkerEntrance = tagged.transform;
            }
            catch { }
        }
    }

    void UpdateCarNameUI()
    {
        if (carNameText == null) return;
        if (cars.Count == 0)
        {
            carNameText.text = "No Cars";
            return;
        }
        carNameText.text = cars[selectedIndex].displayName;
    }

    public void NextCar()
    {
        if (cars.Count == 0) return;
        selectedIndex = (selectedIndex + 1) % cars.Count;
        UpdateCarNameUI();
    }

    public void PreviousCar()
    {
        if (cars.Count == 0) return;
        selectedIndex = (selectedIndex - 1 + cars.Count) % cars.Count;
        UpdateCarNameUI();
    }

    public void Play()
    {
        if (cars.Count == 0)
        {
            Debug.LogWarning("[SpawnCar] No cars to spawn.");
            return;
        }

        // Ensure bunker reference is available if spawning near bunker
        if (spawnNearBunker && bunkerEntrance == null)
        {
            var wg = FindObjectOfType<WorldGenerator>();
            if (wg != null && wg.spawnedBunkerEntrance != null)
            {
                bunkerEntrance = wg.spawnedBunkerEntrance;
            }
        }
        // If still missing and we must spawn near bunker, delay spawning
        if (spawnNearBunker && bunkerEntrance == null)
        {
            Debug.LogWarning("[SpawnCar] Bunker entrance not ready; skipping spawn until world generation completes.");
            return;
        }

        // Remove previously spawned car (optional)
        if (spawnedCar != null)
            Destroy(spawnedCar);

        var entry = cars[selectedIndex];
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[SpawnCar] Prefab missing for car index {selectedIndex}");
            return;
        }

        Vector3 spawnPos;
        Quaternion spawnRot;

        // Always base spawning around the bunker entrance
        if (bunkerEntrance != null)
        {
            ComputeRandomSpawnNearBunker(out spawnPos, out spawnRot);
        }
        else
        {
            // If bunker not found, fall back to origin
            spawnPos = Vector3.zero;
            spawnRot = Quaternion.identity;
        }

        spawnedCar = Instantiate(entry.prefab, spawnPos, spawnRot);

        var followCamera = mainCamera != null ? mainCamera.GetComponent<CameraController>() : null;
        if (followCamera != null)
        {
            followCamera.target = spawnedCar.transform;
            followCamera.enabled = true;
        }
        else
        {
            Debug.LogWarning("[SpawnCar] CameraController not found on mainCamera.");
        }
        Titlesceane.SetActive(false);

        // Enable enemy spawner after player car is spawned
        var spawner = FindObjectOfType<Spawner>();
        if (spawner != null)
        {
            spawner.SetSpawningEnabled(true);
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to avoid leaks
        var wg = FindObjectOfType<WorldGenerator>();
        if (wg != null)
        {
            wg.OnGenerationComplete -= OnWorldGenerationComplete;
        }
    }

    void OnWorldGenerationComplete()
    {
        // Ensure bunker reference updated before spawning
        var wg = FindObjectOfType<WorldGenerator>();
        if (wg != null && wg.spawnedBunkerEntrance != null)
        {
            bunkerEntrance = wg.spawnedBunkerEntrance;
        }
        else
        {
            AutoFindBunkerEntrance();
        }
        // Optionally relocate spawn point now that world is ready
        if (spawnNearBunker && bunkerEntrance != null)
        {
            MoveSpawnPointNearBunker();
        }
        // Spawn the selected car
        Play();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ClampSelectedIndex();
        UpdateCarNameUI();
        bunkerMinDistance = Mathf.Max(0.1f, Mathf.Min(bunkerMinDistance, bunkerMaxDistance));
        bunkerMaxDistance = Mathf.Max(bunkerMinDistance, bunkerMaxDistance);
        if (spawnNearBunker && bunkerEntrance == null)
            AutoFindBunkerEntrance();
    }
#endif

    // Compute a random spawn position near the bunker entrance.
    void ComputeRandomSpawnNearBunker(out Vector3 spawnPos, out Quaternion spawnRot)
    {
        spawnPos = bunkerEntrance != null ? bunkerEntrance.position : Vector3.zero;
        spawnRot = bunkerEntrance != null ? bunkerEntrance.rotation : Quaternion.identity;
        if (bunkerEntrance == null) return;

        // Random radius and angle within arc
        float radius = Random.Range(bunkerMinDistance, bunkerMaxDistance);
        float halfArc = bunkerForwardArc * 0.5f;
        float angleOffset = bunkerForwardArc >= 360f ? Random.Range(0f, 360f) : Random.Range(-halfArc, halfArc);
        Vector3 forwardFlat = Vector3.ProjectOnPlane(bunkerEntrance.forward, Vector3.up).normalized;
        if (forwardFlat == Vector3.zero) forwardFlat = Vector3.forward;
        Vector3 dir = Quaternion.AngleAxis(angleOffset, Vector3.up) * forwardFlat;
        Vector3 candidate = bunkerEntrance.position + dir * radius;

        // Raycast down to find ground
        Vector3 rayOrigin = candidate + Vector3.up * 100f;
        Ray ray = new Ray(rayOrigin, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundRaycastMask, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point + Vector3.up * bunkerHeightOffset;
            if (alignToGroundNormal)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(dir, hit.normal);
                if (flatForward == Vector3.zero) flatForward = dir;
                spawnRot = Quaternion.LookRotation(flatForward, hit.normal);
            }
            else
            {
                spawnRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up);
            }
        }
        else
        {
            // Fallback: terrain height if available
            Terrain t = Terrain.activeTerrain;
            if (t != null)
            {
                float h = t.SampleHeight(candidate);
                spawnPos = new Vector3(candidate.x, h + bunkerHeightOffset, candidate.z);
            }
            else
            {
                spawnPos = candidate;
            }
            spawnRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up);
        }
    }

    // Wait for WorldGenerator to finish, then find bunker and move this GameObject near it
    System.Collections.IEnumerator RelocateAfterWorldGeneration()
    {
        WorldGenerator wg = FindObjectOfType<WorldGenerator>();
        // Immediate check: if already generated and bunker exists
        if (wg != null && wg.spawnedBunkerEntrance != null)
        {
            bunkerEntrance = wg.spawnedBunkerEntrance;
            MoveSpawnPointNearBunker();
            yield break;
        }

        bool generationComplete = false;
        System.Action onComplete = () => generationComplete = true;
        if (wg != null) wg.OnGenerationComplete += onComplete;

        // Wait until generation complete OR bunker appears, with timeout
        float timeout = 60f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (wg != null && wg.spawnedBunkerEntrance != null)
            {
                bunkerEntrance = wg.spawnedBunkerEntrance;
                break;
            }
            if (generationComplete)
            {
                // Try auto-find after completion
                AutoFindBunkerEntrance();
                if (bunkerEntrance != null) break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (wg != null) wg.OnGenerationComplete -= onComplete;

        // Final fallback: auto-find by name prefix
        if (bunkerEntrance == null)
        {
            AutoFindBunkerEntrance();
        }

        if (bunkerEntrance != null)
        {
            MoveSpawnPointNearBunker();
        }
        else
        {
            Debug.LogWarning("[SpawnCar] Bunker entrance not found after world generation; spawn point not relocated.");
        }
    }

    // Move this GameObject (and spawnPoint if assigned) near the bunker
    void MoveSpawnPointNearBunker()
    {
        Vector3 spawnPos; Quaternion spawnRot;
        ComputeRandomSpawnNearBunker(out spawnPos, out spawnRot);

        // Move this GameObject itself near the bunker
        transform.position = spawnPos;
        transform.rotation = spawnRot;
    }
}
