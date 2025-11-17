using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SupplyRun : QuestBase
{
    [Header("Warehouse Discovery")]
    public string warehouseTag = "Warehouse";
    public string warehouseNamePrefix = "Warehouse_";

    [Header("Rules")]
    public float reachDistance = 12f;
    public float minSeparationBetweenPickupAndDrop = 50f;

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Waypoints")]
    public bool showWaypoints = true;
    [Tooltip("Prefab with MissionWaypoint component to indicate pickup/dropoff.")] public GameObject waypointPrefab;
    [Tooltip("Canvas under which quest waypoints will be parented.")] public Canvas waypointCanvas;
    [Tooltip("Optional camera override for waypoint projection.")] public Camera waypointCameraOverride;
    [Tooltip("Color tint for the pickup waypoint.")] public Color pickupColor = Color.cyan;
    [Tooltip("Color tint for the dropoff waypoint.")] public Color dropoffColor = Color.yellow;

    enum State { Idle, ToPickup, ToDropoff, Completed, Failed }
    State _state = State.Idle;
    Transform _pickup;
    Transform _dropoff;
    bool _hasCrate = false;
    Coroutine _runner;
    MissionWaypoint _pickupWp;
    MissionWaypoint _dropoffWp;

    public override void StartQuest(GameObject playerObj)
    {
        base.StartQuest(playerObj);
        if (!IsActive)
            return;

        // Find warehouses in scene
        var warehouses = FindWarehouses();
        if (warehouses.Count < 2)
        {
            Status("Supply Run: not enough warehouses found.");
            if (debugLogs) Debug.LogWarning("SupplyRun: need at least 2 warehouses.", this);
            Complete(false);
            return;
        }

        // Choose pickup = closest to player; dropoff = farthest from pickup (with min separation)
        _pickup = ClosestTo(player != null ? player.transform.position : Vector3.zero, warehouses);
        warehouses.Remove(_pickup);
        _dropoff = FarthestFrom(_pickup.position, warehouses, minSeparationBetweenPickupAndDrop);
        if (_dropoff == null)
        {
            // fallback: any other different one
            _dropoff = warehouses.Count > 0 ? warehouses[0] : null;
        }
        if (_pickup == null || _dropoff == null || _pickup == _dropoff)
        {
            Status("Supply Run: could not choose pickup/dropoff.");
            Complete(false);
            return;
        }

        _hasCrate = false;
        _state = State.ToPickup;
        Status($"Supply Run: Proceed to pickup warehouse '{_pickup.name}'.");
        if (_runner != null) StopCoroutine(_runner);
        _runner = StartCoroutine(Run());

        if (showWaypoints) CreateOrUpdateWaypoints();
    }

    public override void CancelQuest()
    {
        if (_runner != null)
        {
            StopCoroutine(_runner);
            _runner = null;
        }
        _state = State.Idle;
        _pickup = null;
        _dropoff = null;
        _hasCrate = false;
        DestroyWaypoints();
        base.CancelQuest();
    }

    IEnumerator Run()
    {
        while (IsActive)
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("playerCar");
                if (player == null)
                {
                    Status("Supply Run: waiting for player...");
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
            }

            switch (_state)
            {
                case State.ToPickup:
                    TickToPoint(_pickup, false);
                    break;
                case State.ToDropoff:
                    TickToPoint(_dropoff, true);
                    break;
            }

            yield return null;
        }
    }

    void TickToPoint(Transform target, bool delivering)
    {
        if (target == null)
        {
            Status("Supply Run: target missing.");
            _state = State.Failed;
            Complete(false);
            return;
        }
        Vector3 ppos = player.transform.position;
        float d = Vector3.Distance(ppos, target.position);

        if (!delivering)
        {
            Status($"Supply Run: Go to pickup warehouse.");
            if (d <= reachDistance)
            {
                _hasCrate = true;
                _state = State.ToDropoff;
                Status($"Supply Run: Crate loaded! Drive to dropoff.");
                if (debugLogs) Debug.Log("SupplyRun: picked up crate.", this);
                if (showWaypoints) UpdateWaypointStates(afterPickup:true);
            }
        }
        else
        {
            Status($"Supply Run: Deliver crate to warehouse.");
            if (_hasCrate && d <= reachDistance)
            {
                _hasCrate = false;
                _state = State.Completed;
                Status("Supply Run: Delivery complete! Great job.");
                Complete(true);
                if (debugLogs) Debug.Log("SupplyRun: delivered crate.", this);
                DestroyWaypoints();
            }
        }
    }

    List<Transform> FindWarehouses()
    {
        var list = new List<Transform>();
        // 1) Try by tag if any exist
        if (!string.IsNullOrEmpty(warehouseTag))
        {
            var tagged = GameObject.FindGameObjectsWithTag(warehouseTag);
            if (tagged != null && tagged.Length > 0)
            {
                foreach (var go in tagged)
                {
                    if (go != null && go.activeInHierarchy)
                        list.Add(go.transform);
                }
            }
        }

        // 2) Fallback by name prefix search across all root objects
        if (list.Count < 2 && !string.IsNullOrEmpty(warehouseNamePrefix))
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t != null && t.gameObject.activeInHierarchy && t.name.StartsWith(warehouseNamePrefix))
                    list.Add(t);
            }
        }

        // Deduplicate
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null) list.RemoveAt(i);
        }
        return list;
    }

    static Transform ClosestTo(Vector3 pos, List<Transform> items)
    {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (var t in items)
        {
            if (t == null) continue;
            float d = Vector3.Distance(pos, t.position);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    static Transform FarthestFrom(Vector3 pos, List<Transform> items, float minSep)
    {
        Transform best = null;
        float bestD = -1f;
        foreach (var t in items)
        {
            if (t == null) continue;
            float d = Vector3.Distance(pos, t.position);
            if (d >= minSep && d > bestD) { bestD = d; best = t; }
        }
        return best;
    }

    void CreateOrUpdateWaypoints()
    {
        if (!showWaypoints || waypointPrefab == null) return;
        if (_pickup != null && _pickupWp == null)
            _pickupWp = SpawnWaypoint(_pickup, pickupColor, "Pickup");
        if (_dropoff != null && _dropoffWp == null)
            _dropoffWp = SpawnWaypoint(_dropoff, dropoffColor, "Dropoff");
        UpdateWaypointStates(afterPickup:false);
    }

    void UpdateWaypointStates(bool afterPickup)
    {
        // Before pickup: highlight pickup, dim/dropoff
        if (_pickupWp != null)
            _pickupWp.gameObject.SetActive(!afterPickup); // hide after pickup
        if (_dropoffWp != null)
            _dropoffWp.gameObject.SetActive(afterPickup); // show only after pickup
    }

    MissionWaypoint SpawnWaypoint(Transform target, Color tint, string labelSuffix)
    {
        var parent = waypointCanvas != null ? waypointCanvas.transform : null;
        var inst = Instantiate(waypointPrefab, parent);
        var mw = inst.GetComponent<MissionWaypoint>();
        if (mw == null) mw = inst.GetComponentInChildren<MissionWaypoint>();
        if (mw == null)
        {
            Debug.LogError("SupplyRun: waypointPrefab lacks MissionWaypoint component.", inst);
            Destroy(inst);
            return null;
        }
        mw.target = target;
        if (waypointCameraOverride != null) mw.cam = waypointCameraOverride; else if (mw.cam == null) mw.cam = Camera.main;
        if (mw.canvas == null && waypointCanvas != null) mw.canvas = waypointCanvas;
        // Try to set color and label if available
        var img = inst.GetComponent<UnityEngine.UI.Image>();
        if (img == null) img = inst.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img != null) img.color = tint;
        var txt = inst.GetComponent<TMPro.TMP_Text>();
        if (txt == null) txt = inst.GetComponentInChildren<TMPro.TMP_Text>();
        if (txt != null)
        {
            string baseName = target != null ? target.name : labelSuffix;
            txt.text = baseName + " (" + labelSuffix + ")";
        }
        return mw;
    }

    void DestroyWaypoints()
    {
        if (_pickupWp != null) Destroy(_pickupWp.gameObject); _pickupWp = null;
        if (_dropoffWp != null) Destroy(_dropoffWp.gameObject); _dropoffWp = null;
    }
}
