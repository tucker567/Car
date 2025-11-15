using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager that shows a MissionWaypoint UI for nearby cell towers.
/// No waypoint scripts are required on individual towers.
/// </summary>
public class CellTowerWaypointManager : MonoBehaviour
{
    [Header("Discovery")]
    [Tooltip("Include objects whose name starts with the given prefix (default 'CellTower_').")] public bool includeNamePrefix = true;
    [Tooltip("Name prefix to use when includeNamePrefix is enabled.")] public string towerNamePrefix = "CellTower_";
    [Tooltip("Rescan interval in seconds for newly spawned towers (0 = only at start). ")] public float rescanInterval = 5f;

    [Header("Activation")]
    [Tooltip("Distance at or below which a tower gets a waypoint.")] public float activationRadius = 250f;
    [Tooltip("Distance at or below which waypoints hide entirely (set negative to disable). ")] public float hideRadius = -1f;

    [Header("References")]
    [Tooltip("Prefab containing a MissionWaypoint component.")] public GameObject waypointPrefab;
    [Tooltip("Canvas under which waypoints will be parented.")] public Canvas canvas;
    [Tooltip("Override camera for waypoint projection (optional). Use Camera.main if null.")] public Camera overrideCamera;

    [Header("Performance & Pooling")]
    [Tooltip("Max active waypoints (nearest towers prioritized). 0 = unlimited.")] public int maxActiveWaypoints = 0;
    [Tooltip("Seconds between distance refreshes (0 = every frame). ")] public float distanceUpdateInterval = 0f;

    [Header("Player Auto-Find")]
    [Tooltip("If true, manager will keep trying to find the player by tag at runtime.")] public bool autoFindPlayer = true;
    [Tooltip("Tag used to locate player if not assigned.")] public string playerTag = "playerCar";
    [Tooltip("Seconds between player search attempts.")] public float playerSearchInterval = 0.5f;

    private readonly List<Transform> _towers = new List<Transform>();
    private readonly Dictionary<Transform, MissionWaypoint> _active = new Dictionary<Transform, MissionWaypoint>();
    private float _nextScanTime;
    private float _nextDistUpdateTime;
    public Transform player;

    // Internal player search bookkeeping
    private float _nextPlayerSearchTime;
    private bool _playerSearchStarted;

    void Awake()
    {
        // Delay player search so late-spawned cars can appear; initial scan can run regardless.
        InitialScan();
    }

    void InitialScan() => ScanForTowers(force:true);

    void Update()
    {
        // Late player spawn support
        if (player == null && autoFindPlayer)
        {
            if (!_playerSearchStarted)
            {
                _playerSearchStarted = true;
                _nextPlayerSearchTime = Time.time; // search immediately first frame
            }
            if (Time.time >= _nextPlayerSearchTime)
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    player = tagged.transform;
                    Debug.Log("CellTowerWaypointManager: Attached to player '" + player.name + "' via tag '" + playerTag + "'.");
                }
                _nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);
            }
        }

        if (player == null || waypointPrefab == null) return;

        // Rescan for newly spawned towers
        if (rescanInterval > 0f && Time.time >= _nextScanTime)
        {
            ScanForTowers(force:false);
            _nextScanTime = Time.time + rescanInterval;
        }

        // Distance refresh cadence
        if (distanceUpdateInterval > 0f && Time.time < _nextDistUpdateTime) return;
        _nextDistUpdateTime = Time.time + Mathf.Max(0f, distanceUpdateInterval);

        // Build list of distances
        var playerPos = player.position;
        var chosen = new List<(Transform tower, float dist)>();
        foreach (var t in _towers)
        {
            if (t == null) continue;
            float d = Vector3.Distance(playerPos, t.position);
            if (hideRadius >= 0f && d <= hideRadius) continue; // hide region
            if (d <= activationRadius) chosen.Add((t, d));
        }

        // Prioritize nearest
        chosen.Sort((a, b) => a.dist.CompareTo(b.dist));
        if (maxActiveWaypoints > 0 && chosen.Count > maxActiveWaypoints)
            chosen = chosen.GetRange(0, maxActiveWaypoints);

        // Activate required waypoints
        var keep = new HashSet<Transform>();
        foreach (var (tower, dist) in chosen)
        {
            keep.Add(tower);
            if (!_active.ContainsKey(tower))
                CreateWaypoint(tower);
            else
                _active[tower].gameObject.SetActive(true);
        }

        // Deactivate extras
        var toRemove = new List<Transform>();
        foreach (var kv in _active)
        {
            if (!keep.Contains(kv.Key))
            {
                kv.Value.gameObject.SetActive(false);
            }
            if (kv.Key == null)
                toRemove.Add(kv.Key);
        }
        foreach (var dead in toRemove) _active.Remove(dead);
    }

    void ScanForTowers(bool force)
    {
        if (!includeNamePrefix && !force) return;
        var found = new HashSet<Transform>();

        if (includeNamePrefix)
        {
            Transform[] all;
#if UNITY_2023_1_OR_NEWER
            all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            all = Object.FindObjectsOfType<Transform>();
#endif
            foreach (var tr in all)
            {
                if (tr != null && tr.name.StartsWith(towerNamePrefix)) found.Add(tr);
            }
        }
        _towers.Clear();
        _towers.AddRange(found);
    }

    void CreateWaypoint(Transform tower)
    {
        var parent = canvas != null ? canvas.transform : null;
        var inst = Instantiate(waypointPrefab, parent);
        var mw = inst.GetComponent<MissionWaypoint>();
        if (mw == null) mw = inst.GetComponentInChildren<MissionWaypoint>();
        if (mw == null)
        {
            Debug.LogError("CellTowerWaypointManager: waypointPrefab lacks MissionWaypoint component.", inst);
            Destroy(inst);
            return;
        }
        mw.target = tower;
        if (overrideCamera != null) mw.cam = overrideCamera; else if (mw.cam == null) mw.cam = Camera.main;
        if (mw.canvas == null && canvas != null) mw.canvas = canvas;
        _active[tower] = mw;
    }
}
