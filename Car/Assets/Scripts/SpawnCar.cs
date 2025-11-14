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
    public Transform spawnPoint;
    public Camera mainCamera;

    // Runtime reference to the spawned car
    private GameObject spawnedCar;

    void Awake()
    {
        AutoFindCarNameText();
        ClampSelectedIndex();
        UpdateCarNameUI();
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

        // Remove previously spawned car (optional)
        if (spawnedCar != null)
            Destroy(spawnedCar);

        var entry = cars[selectedIndex];
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[SpawnCar] Prefab missing for car index {selectedIndex}");
            return;
        }

        var spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        var spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

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
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ClampSelectedIndex();
        UpdateCarNameUI();
    }
#endif
}
