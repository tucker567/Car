using UnityEngine;

public class WoldUnloader : MonoBehaviour
{
    [Header("Bunker Settings")]
    public string bunkerTag = "Bunker";
    public GameObject bunkerObject;
    [Tooltip("Charge rate per second while inside bunker trigger.")]
    public float chargeRatePerSecond = 50f;
    [Tooltip("Maximum charge value.")]
    public float maxCharge = 100f;

    [Header("Player Settings")]
    public string playerTag = "playerCar";
    public bool autoFindPlayer = true;

    [Header("UI Settings")]
    public GameObject UIPanel;
    public TMPro.TMP_Text UIChargeText;
    [Tooltip("When closed via button, seconds to keep panel hidden.")]
    public float manualCloseDuration = 5f;

    [Header("Other Settings")]
    public QuestPickerManager questPickerManager;
    [Tooltip("When true, another system (e.g., QuestPickerManager) controls UIChargeText visibility and content.")]
    public bool allowExternalChargeTextControl = false;

    public Transform player;
    public float charge;
    private bool uiShown; // no longer gates showing; kept for backward compatibility
    private bool insideBunker;
    private float reopenAtTime = -1f;
    private bool panelManuallyClosed = false;

    // Expose bunker state for coordination with other systems
    public bool IsInsideBunker => insideBunker;

    void Awake()
    {
        if (UIPanel != null) UIPanel.SetActive(false);
        UpdateChargeUI();
    }

    void Update()
    {
        // Auto-find player if enabled
        if (player == null && autoFindPlayer)
        {
            var tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null)
            {
                player = tagged.transform;
                Debug.Log("WoldUnloader: Attached to player '" + player.name + "' via tag '" + playerTag + "'.");
            }
        }

        // Auto-find bunker object if not set
        if (bunkerObject == null)
        {
            var bunker = GameObject.FindGameObjectWithTag(bunkerTag);
            if (bunker != null)
            {
                bunkerObject = bunker;
                Debug.Log("WoldUnloader: Found bunker object '" + bunkerObject.name + "' via tag '" + bunkerTag + "'.");
            }
        }

        // Handle timed re-open if panel was closed via button and player remains inside
        if (UIPanel != null && !UIPanel.activeSelf && insideBunker && reopenAtTime > 0f && Time.time >= reopenAtTime)
        {
            UIPanel.SetActive(true);
            var popUpCanvas = GameObject.Find("PopUpUI - Canvas");
            if (popUpCanvas != null) popUpCanvas.SetActive(false);
            var gameplayCanvas = GameObject.Find("GamePlay - Canvas");
            if (gameplayCanvas != null) gameplayCanvas.SetActive(false);
            reopenAtTime = -1f;
            Debug.Log("WoldUnloader: Timed reopen of panel.");
        }

        // Continuously evaluate whether player is inside bunker volume
        bool wasInside = insideBunker;
        insideBunker = false;
        if (bunkerObject != null && bunkerObject.TryGetComponent<Collider>(out var col))
        {
            // Ensure player reference
            if (player == null && autoFindPlayer)
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null) player = tagged.transform;
            }
            if (player != null)
            {
                // Use collider bounds to determine containment to avoid jitter
                insideBunker = col.bounds.Contains(player.position);
                if (insideBunker)
                {
                    // Charge up while inside
                    charge = Mathf.Min(maxCharge, charge + chargeRatePerSecond * Time.deltaTime);
                    UpdateChargeUI();
                    // Show bunker panel whenever fully charged and currently hidden
                    if (charge >= maxCharge && UIPanel != null && !UIPanel.activeSelf && !panelManuallyClosed)
                    {
                        UIPanel.SetActive(true);
                        var popUpCanvas = GameObject.Find("PopUpUI - Canvas");
                        if (popUpCanvas != null) popUpCanvas.SetActive(false);
                        var gameplayCanvas = GameObject.Find("GamePlay - Canvas");
                        if (gameplayCanvas != null) gameplayCanvas.SetActive(false);
                        Debug.Log("WoldUnloader: Charge complete. UI enabled.");
                    }
                }
            }
        }

        // Handle leaving bunker (no auto-close; keep panel state)
        if (wasInside && !insideBunker)
        {
            reopenAtTime = -1f;
            panelManuallyClosed = false; // allow future auto-open after exit/enter cycle
        }
        
        if (!allowExternalChargeTextControl)
        {
            // if charge is greater than 0 enable charge text UI
            if (charge > 0f && UIChargeText != null && !UIChargeText.gameObject.activeSelf)
            {
                UIChargeText.gameObject.SetActive(true);
            }
            // if charge is 0 disable charge text UI
            else if (charge <= 0f && UIChargeText != null && UIChargeText.gameObject.activeSelf)
            {
                UIChargeText.gameObject.SetActive(false);
            }
        }

        // Decay charge when player is not inside bunker
        if (!insideBunker && charge > 0f)
        {
            charge = Mathf.Max(0f, charge - chargeRatePerSecond * Time.deltaTime);
            UpdateChargeUI();
        }
    }
    
    // Remove reliance on Unity's trigger callbacks; handled in Update.

    // OnTriggerExit is no longer used for auto-closing; Update handles state.

    void UpdateChargeUI()
    {
        if (allowExternalChargeTextControl) return; // another system manages the text content
        if (UIChargeText != null)
        {
            UIChargeText.text = Mathf.RoundToInt(charge) + "%";
        }
    }

    // Button: close the panel for a while (manualCloseDuration)
    public void ClosePanelTemporarily()
    {
        if (UIPanel == null) return;
        UIPanel.SetActive(false);
        // Schedule reopen only if player still inside and already fully charged
        if (insideBunker && charge >= maxCharge)
        {
            reopenAtTime = Time.time + manualCloseDuration;
            panelManuallyClosed = false; // temporary close should auto re-open
            Debug.Log("WoldUnloader: Panel closed temporarily for " + manualCloseDuration + "s.");
        }
        else
        {
            reopenAtTime = -1f;
            panelManuallyClosed = true;
        }
        // Set the charge to zero upon closing the panel
        charge = 0f;
        UpdateChargeUI();
    }

    // Optional Button: immediately close with no timed reopen
    public void ClosePanelNow()
    {
        if (UIPanel == null) return;
        UIPanel.SetActive(false);
        reopenAtTime = -1f;
        // Allow future reopen when conditions are met
        uiShown = false;
        panelManuallyClosed = true; // suppress auto-open until exit/enter or explicit reset
        Debug.Log("WoldUnloader: Panel closed.");
    }

    // Find all cars and aicars in the scene and remove them
    public void UnloadAllCars()
    {
        var cars = GameObject.FindGameObjectsWithTag("playerCar");
        var aiCars = GameObject.FindGameObjectsWithTag("AICar");
        int totalRemoved = 0;

        foreach (var car in cars)
        {
            Destroy(car);
            totalRemoved++;
        }
        foreach (var aiCar in aiCars)
        {
            Destroy(aiCar);
            totalRemoved++;
        }

        Debug.Log("WoldUnloader: Unloaded " + totalRemoved + " cars from the scene.");
        charge = 0f;
        UpdateChargeUI();
    }

    public void Quitapplication()
    {
        Debug.Log("WoldUnloader: Quitting application.");
        Application.Quit();
    }
}