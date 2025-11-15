using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class QuestPickerManager : MonoBehaviour
{
    // Auto-discovery settings
    [Header("Auto Discovery Settings")]
    public bool autoDiscover = true;                 // Enable runtime scan for new towers
    public string towerTag = "CellTower";            // Tag used by tower prefabs
    public float refreshInterval = 2f;               // Seconds between discovery scans
    // Note: Only active objects are discovered; inactive are ignored.
    [Tooltip("If true, use distance-to-center checks instead of collider overlap.")]
    public bool useDistanceCheck = true;
    [Tooltip("Fallback range (meters) when a tower has no SphereCollider or when collider radius is not preferred.")]
    public float range = 25f;
    [Tooltip("When true and a SphereCollider exists, use its world radius for the range.")]
    public bool preferColliderRadius = true;

    // Internal list of discovered trigger colliders.
    private readonly List<SphereCollider> triggerAreas = new List<SphereCollider>();
    private readonly List<Transform> towerRoots = new List<Transform>();

    // UI references
    public VisualElement questListContainer;
    public TMP_Text towerchargeText;

    private float _nextRefreshTime;
    private float _currentCharge = 0f;
    private GameObject _player;
    [Header("Debug")]
    public bool debugLogs = false;
    private bool _wasInsideAny = false;

    void Awake()
    {
        if (questListContainer != null)
            questListContainer.style.display = DisplayStyle.None;
        if (towerchargeText != null)
        {
            towerchargeText.gameObject.SetActive(false);
            towerchargeText.text = "0";
        }
        _nextRefreshTime = Time.time + 0.25f; // Quick initial scan shortly after start
    }

    void Update()
    {
        if (autoDiscover && Time.time >= _nextRefreshTime)
        {
            RefreshTriggerAreas();
            _nextRefreshTime = Time.time + refreshInterval;
        }

        if (triggerAreas.Count == 0)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("playerCar");
        if (player == null) return;

        _player = player;

        bool insideAny = false;
        // Clean out any destroyed colliders while checking distance/containment
        if (triggerAreas.Count > 0)
        {
            for (int i = triggerAreas.Count - 1; i >= 0; i--)
            {
                var area = triggerAreas[i];
                if (area == null)
                {
                    triggerAreas.RemoveAt(i);
                    continue;
                }
                if (!area.gameObject.activeInHierarchy)
                {
                    triggerAreas.RemoveAt(i);
                    continue;
                }

                if (useDistanceCheck)
                {
                    if (WithinRange(area, _player.transform.position))
                    {
                        insideAny = true;
                        break;
                    }
                }
                else
                {
                    if (area.bounds.Contains(_player.transform.position))
                    {
                        insideAny = true;
                        break;
                    }
                }
            }
        }
        else if (towerRoots.Count > 0 && useDistanceCheck)
        {
            for (int i = towerRoots.Count - 1; i >= 0; i--)
            {
                var t = towerRoots[i];
                if (t == null || !t.gameObject.activeInHierarchy)
                {
                    towerRoots.RemoveAt(i);
                    continue;
                }
                if (!string.IsNullOrEmpty(towerTag) && !t.CompareTag(towerTag))
                {
                    towerRoots.RemoveAt(i);
                    continue;
                }
                if (Vector3.Distance(_player.transform.position, t.position) <= range)
                {
                    insideAny = true;
                    break;
                }
            }
        }

        if (debugLogs && insideAny != _wasInsideAny)
        {
            Debug.Log($"QuestPickerManager: player {(insideAny ? "entered" : "exited")} tower area (tracking {triggerAreas.Count}).", this);
            _wasInsideAny = insideAny;
        }

        if (insideAny)
        {
            if (_currentCharge < 100f)
            {
                _currentCharge += Time.deltaTime * 20f; // Charge rate
                _currentCharge = Mathf.Min(_currentCharge, 100f);
                if (towerchargeText != null)
                {
                    towerchargeText.gameObject.SetActive(true);
                    towerchargeText.text = Mathf.FloorToInt(_currentCharge).ToString();
                }
                if (questListContainer != null && _currentCharge < 100f)
                    questListContainer.style.display = DisplayStyle.None;
            }
            else
            {
                if (questListContainer != null)
                    questListContainer.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            _currentCharge = 0f;
            if (towerchargeText != null)
            {
                towerchargeText.text = "0";
                towerchargeText.gameObject.SetActive(false);
            }
            if (questListContainer != null)
                questListContainer.style.display = DisplayStyle.None;
        }
    }

    // Scans the scene for SphereColliders on GameObjects matching tag criteria.
    private void RefreshTriggerAreas()
    {
        // Simple approach: find all objects with the towerTag, else fallback to all SphereColliders.
        var found = new List<SphereCollider>();
        towerRoots.Clear();
        if (!string.IsNullOrEmpty(towerTag))
        {
            var taggedObjects = GameObject.FindGameObjectsWithTag(towerTag);
            foreach (var go in taggedObjects)
            {
                towerRoots.Add(go.transform);
                // Include colliders on children as many towers place colliders on child nodes.
                var colliders = go.GetComponentsInChildren<SphereCollider>(false);
                if (colliders != null && colliders.Length > 0)
                    found.AddRange(colliders);
            }
        }
        else
        {
            // Use non-obsolete API; choose unsorted for performance.
            var all = Object.FindObjectsByType<SphereCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var sc in all)
            {
                var go = sc.gameObject;
                found.Add(sc);
            }
        }

        // Merge: add new colliders not already tracked
        foreach (var sc in found)
        {
            if (!triggerAreas.Contains(sc))
                triggerAreas.Add(sc);
        }

        // Remove any that no longer exist or no longer meet criteria
        for (int i = triggerAreas.Count - 1; i >= 0; i--)
        {
            var sc = triggerAreas[i];
            if (sc == null)
            {
                triggerAreas.RemoveAt(i);
                continue;
            }
            var go = sc.gameObject;
            if (!go.activeInHierarchy)
            {
                triggerAreas.RemoveAt(i);
                continue;
            }
            if (!string.IsNullOrEmpty(towerTag) && !go.CompareTag(towerTag))
            {
                triggerAreas.RemoveAt(i);
                continue;
            }
        }
    }

    // Returns true if the player's position is within range of the sphere collider's world center.
    private bool WithinRange(SphereCollider area, Vector3 playerPos)
    {
        if (area == null) return false;
        var center = area.transform.TransformPoint(area.center);
        float effectiveRange = range;
        if (preferColliderRadius)
        {
            float maxScale = Mathf.Max(Mathf.Abs(area.transform.lossyScale.x), Mathf.Abs(area.transform.lossyScale.y), Mathf.Abs(area.transform.lossyScale.z));
            effectiveRange = Mathf.Max(range, area.radius * maxScale);
        }
        return Vector3.Distance(playerPos, center) <= effectiveRange;
    }
}
