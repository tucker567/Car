using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class QuestPickerManager : MonoBehaviour
{
    // Auto-discovery settings
    [Header("Auto Discovery Settings")]
    public bool autoDiscover = true;                 // Enable runtime scan for new towers
    public string towerTag = "CellTower";            // Tag used by tower prefabs
    public float refreshInterval = 2f;               // Seconds between discovery scans
    [Tooltip("If true, manager will keep trying to find the player by tag at runtime.")] public bool autoFindPlayer = true;
    [Tooltip("Tag used to locate player if not assigned.")] public string playerTag = "playerCar";
    [Tooltip("Seconds between player search attempts.")] public float playerSearchInterval = 0.5f;
    public GameObject playerObject;                    // Optional direct reference to player object
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

    [Header("Quest UI (UGUI)")]
    public Canvas questCanvas;                 // Quest canvas containing both panels
    public GameObject panelSelectQuest;        // First panel: selection list
    public Transform buttonContainer;          // Where buttons are spawned (under panelSelectQuest)
    public GameObject questButtonPrefab;       // Prefab with Button + TMP_Text child
    public GameObject panelQuestActive;        // Second panel: active quest display
    public TMP_Text activeQuestText;           // Text component to show current quest

    [Header("Quests")] 
    public List<QuestBase> quests = new List<QuestBase>();
    public float cooldownSeconds = 30f;        // Time after selection before charging is allowed again

    [Header("Button Layout")] 
    public bool autoLayoutButtons = true;      // Ensure a VerticalLayoutGroup exists
    public float buttonSpacing = 8f;           // Spacing between buttons
    public int paddingLeft = 8;                // Container padding
    public int paddingRight = 8;
    public int paddingTop = 8;
    public int paddingBottom = 8;
    public bool controlChildWidth = true;      // Layout controls child width
    public bool controlChildHeight = true;     // Layout controls child height
    public bool expandChildWidth = true;       // Force expand width
    public bool expandChildHeight = false;     // Force expand height
    public bool setButtonPreferredHeight = true;
    public float buttonPreferredHeight = 40f;

    [Header("Quest Switching")]
    public bool allowQuestSwitching = true;   // Allow player to change quest after one is active

    [Header("UI Behavior")]
    [Tooltip("Seconds to keep active quest panel visible after completion before hiding.")] public float activePanelHideDelayAfterComplete = 3f;
    [Tooltip("Hide panel on failed quest as well.")] public bool hidePanelOnFail = true;
    [Tooltip("If false, QuestPickerManager will not touch the tower charge text UI.")]
    public bool enableTowerChargeText = true;

    [Header("Scoring")]
    [Tooltip("Points awarded to the player when a quest is completed successfully.")]
    public int questCompletionScore = 1000;
    [Tooltip("If true, show the reward amount in the active quest text on completion.")]
    public bool showRewardOnCompleteText = true;

    [Header("Other Settings")]
    public WoldUnloader woldUnloader;

    private Coroutine _hidePanelRoutine;

    private float _nextRefreshTime;
    public float _currentCharge = 0f;
    private GameObject _player;
    private float _nextChargeAllowedTime = 0f;
    private bool _selectionSpawned = false;
    private int _currentQuestIndex = -1;
    [Header("Debug")]
    public bool debugLogs = false;
    private bool _wasInsideAny = false;
    private bool _playerSearchStarted = false; // Initialize the player search flag
    private float _nextPlayerSearchTime = 0f; // Initialize the variable to track the next player search time

    void Awake()
    {
        if (questListContainer != null)
            questListContainer.style.display = DisplayStyle.None;
        if (towerchargeText != null && ShouldManageTowerChargeText())
        {
            towerchargeText.gameObject.SetActive(false);
            towerchargeText.text = "0%";
        }
        // Initialize quest canvas and panels
        if (questCanvas != null)
            questCanvas.enabled = false;
        if (panelSelectQuest != null)
            panelSelectQuest.SetActive(false);
        if (panelQuestActive != null)
            panelQuestActive.SetActive(false);
        EnsureButtonLayout();
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

        if (_player == null && autoFindPlayer)
        {
            if (!_playerSearchStarted)
            {
                _playerSearchStarted = true;
                _nextPlayerSearchTime = Time.time; // Search immediately on the first frame
            }
            if (Time.time >= _nextPlayerSearchTime)
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    _player = tagged;
                    Debug.Log($"QuestPickerManager: Player found and assigned: {_player.name}");
                    // asine playerObject reference if not already set
                    if (playerObject == null)   
                        playerObject = _player;
                }
                else if (debugLogs)
                {
                    Debug.LogWarning($"QuestPickerManager: Player with tag '{playerTag}' not found. Retrying in {playerSearchInterval} seconds.");
                }
                _nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);
            }
        }

        if (_player == null)
            return;

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

        bool inCooldown = Time.time < _nextChargeAllowedTime;

        if (insideAny && !inCooldown)
        {
            if (_currentCharge < 100f)
            {
                _currentCharge += Time.deltaTime * 20f; // Charge rate
                _currentCharge = Mathf.Min(_currentCharge, 100f);
                // If sharing the charge text with WoldUnloader and we're outside the bunker,
                // take ownership so WoldUnloader doesn't hide or overwrite the text.
                if (IsSharedWithWoldUnloader() && woldUnloader != null && !woldUnloader.IsInsideBunker)
                {
                    woldUnloader.allowExternalChargeTextControl = true;
                }
                if (towerchargeText != null && ShouldManageTowerChargeText())
                {
                    towerchargeText.gameObject.SetActive(true);
                    towerchargeText.text = Mathf.FloorToInt(_currentCharge).ToString() + "%";
                }
                if (questListContainer != null && _currentCharge < 100f)
                    questListContainer.style.display = DisplayStyle.None;
            }
            else
            {
                // Fully charged: open selection panel and spawn buttons once
                ShowQuestSelection();
            }
        }
        else
        {
            // Release ownership back to WoldUnloader when not managing
            if (IsSharedWithWoldUnloader() && woldUnloader != null)
            {
                woldUnloader.allowExternalChargeTextControl = false;
            }
            _currentCharge = 0f;
            if (towerchargeText != null && ShouldManageTowerChargeText())
            {
                towerchargeText.text = "0%";
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

    // UI flow: show selection panel and spawn quest buttons
    private void ShowQuestSelection()
    {
        // If switching is allowed, ignore active quest index when showing selection.
        if (_selectionSpawned) return;
        if (!allowQuestSwitching && _currentQuestIndex >= 0) return;
        if (questCanvas != null) questCanvas.enabled = true;
        if (panelQuestActive != null) panelQuestActive.SetActive(false);
        if (panelSelectQuest != null) panelSelectQuest.SetActive(true);
        SpawnQuestButtons();
        _selectionSpawned = true;
    }

    private void SpawnQuestButtons()
    {
        if (buttonContainer == null || questButtonPrefab == null) return;
        // Clear old buttons
        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
        {
            var child = buttonContainer.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
        EnsureButtonLayout();
        for (int i = 0; i < quests.Count; i++)
        {
            int idx = i; // capture for closure
            var go = Instantiate(questButtonPrefab, buttonContainer);
            var btn = go.GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn != null)
                btn.onClick.AddListener(() => StartQuest(idx));
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                // Mark current quest if switching allowed.
                string qname = quests[i] != null ? quests[i].DisplayName : "(Missing Quest)";
                var qref = quests[i];
                if (allowQuestSwitching && _currentQuestIndex == idx && qref != null && qref.IsActive)
                {
                    label.text = qname + " (Current)";
                }
                else
                {
                    label.text = qname;
                }
            }
            // Ensure each button participates nicely in layout
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y);
                rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y);
            }
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (setButtonPreferredHeight)
            {
                le.preferredHeight = buttonPreferredHeight;
                le.minHeight = buttonPreferredHeight;
            }
        }
    }

    public void StartQuest(int questIndex)
    {
        if (questIndex < 0 || questIndex >= quests.Count) return;
        var quest = quests[questIndex];
        if (quest == null) return;

        // Stop previous quest if switching
        if (_currentQuestIndex >= 0 && _currentQuestIndex < quests.Count)
        {
            var prev = quests[_currentQuestIndex];
            if (prev != null && prev.IsActive && allowQuestSwitching)
            {
                prev.CancelQuest();
                prev.OnStatus -= OnQuestStatus;
                prev.OnCompleted -= OnQuestCompleted;
            }
        }

        _currentQuestIndex = questIndex;
        _nextChargeAllowedTime = Time.time + cooldownSeconds;
        _currentCharge = 0f;
        if (towerchargeText != null)
        {
            towerchargeText.text = "0";
            towerchargeText.gameObject.SetActive(false);
        }
        // Swap panels
        if (panelSelectQuest != null) panelSelectQuest.SetActive(false);
        if (panelQuestActive != null) panelQuestActive.SetActive(true);
        if (questCanvas != null) questCanvas.enabled = true;
        if (activeQuestText != null) activeQuestText.text = quest.DisplayName;
        _selectionSpawned = false; // allow re-spawn next time charge reaches 100

        // Subscribe to quest updates
        quest.OnStatus -= OnQuestStatus;
        quest.OnCompleted -= OnQuestCompleted;
        quest.OnStatus += OnQuestStatus;
        quest.OnCompleted += OnQuestCompleted;

        // Start quest with current player reference
        var playerObj = _player != null ? _player : GameObject.FindGameObjectWithTag("playerCar");
        quest.StartQuest(playerObj);
    }

    private void OnQuestStatus(string msg)
    {
        if (activeQuestText != null && !string.IsNullOrEmpty(msg))
            activeQuestText.text = msg;
    }

    private Coroutine _showCompletionRoutine;
    private IEnumerator ShowQuestCompletionMessages(bool success)
    {
        if (success)
        {
            // Display quest complete message
            if (activeQuestText != null)
            {
                activeQuestText.text = $"Quest complete! +{questCompletionScore}pts";
            }

            // Wait for a short delay
            yield return new WaitForSeconds(2f);

            // Display healing message
            if (activeQuestText != null)
            {
                activeQuestText.text = "You have been healed!";
            }

            // Heal the player
            var playerHealth = _player != null ? _player.GetComponent<CarHealth>() : null;
            if (playerHealth != null)
            {
                playerHealth.HealToFull();
            }

            // Wait for the configured delay before hiding the panel
            yield return new WaitForSeconds(activePanelHideDelayAfterComplete);

            if (panelQuestActive != null)
            {
                panelQuestActive.SetActive(false);
            }
        }
        else
        {
            if (activeQuestText != null)
            {
                activeQuestText.text = "Quest failed.";
            }

            if (hidePanelOnFail && activePanelHideDelayAfterComplete > 0f)
            {
                yield return new WaitForSeconds(activePanelHideDelayAfterComplete);
                if (panelQuestActive != null)
                {
                    panelQuestActive.SetActive(false);
                }
            }
        }

        _hidePanelRoutine = null;
    }

    private void OnQuestCompleted(bool success)
    {
        if (_hidePanelRoutine != null)
        {
            StopCoroutine(_hidePanelRoutine);
            _hidePanelRoutine = null;
        }

        if (success)
        {
            // Award score for successful quest completion
            var score = FindObjectOfType<Score>();
            if (score != null)
            {
                score.AddQuestBonus(questCompletionScore);
            }
        }

        // Start the coroutine to show messages and hide the panel
        _hidePanelRoutine = StartCoroutine(ShowQuestCompletionMessages(success));
    }

    private IEnumerator HideActivePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panelQuestActive != null)
            panelQuestActive.SetActive(false);
        _hidePanelRoutine = null;
    }

    // Ensures a VerticalLayoutGroup + ContentSizeFitter on the container with configured spacing/padding
    private void EnsureButtonLayout()
    {
        if (!autoLayoutButtons || buttonContainer == null) return;
        var v = buttonContainer.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = buttonContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = buttonSpacing;
        v.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        v.childControlWidth = controlChildWidth;
        v.childControlHeight = controlChildHeight;
        v.childForceExpandWidth = expandChildWidth;
        v.childForceExpandHeight = expandChildHeight;

        var fitter = buttonContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = buttonContainer.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // If QuestPickerManager's towerchargeText is the same object managed by WoldUnloader.UIChargeText,
    // avoid changing its visibility/text to prevent conflicts.
    private bool ShouldManageTowerChargeText()
    {
        if (!enableTowerChargeText) return false;
        // If shared with WoldUnloader, only manage it when outside the bunker
        if (IsSharedWithWoldUnloader())
        {
            return woldUnloader != null && !woldUnloader.IsInsideBunker;
        }
        return true;
    }

    private bool IsSharedWithWoldUnloader()
    {
        return woldUnloader != null && woldUnloader.UIChargeText != null && towerchargeText == woldUnloader.UIChargeText;
    }
}
