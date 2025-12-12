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

    public Transform player;
    public float charge;
    private bool uiShown;
    private bool insideBunker;
    private float reopenAtTime = -1f;

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
        if (UIPanel != null && !UIPanel.activeSelf && uiShown && insideBunker && reopenAtTime > 0f && Time.time >= reopenAtTime)
        {
            UIPanel.SetActive(true);
            reopenAtTime = -1f;
            Debug.Log("WoldUnloader: Timed reopen of panel.");
        }

        if (bunkerObject != null && bunkerObject.TryGetComponent<Collider>(out var col))
        {
            OnTriggerStay(col);
        }
    }

    void OnTriggerStay(Collider other)
    {
        // Require bunker trigger volume
        if (!other.CompareTag(bunkerTag)) return;

        // Ensure player exists and is the one inside
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null) player = tagged.transform;
        }
        if (player == null) return;

        // Only charge when the player is inside this trigger
        // Using distance check to player's position vs closest point
        Vector3 cp = other.ClosestPoint(player.position);
        if ((cp - player.position).sqrMagnitude < 0.01f)
        {
            insideBunker = true;
            charge = Mathf.Min(maxCharge, charge + chargeRatePerSecond * Time.deltaTime);
            UpdateChargeUI();
            if (!uiShown && charge >= maxCharge)
            {
                uiShown = true;
                if (UIPanel != null) UIPanel.SetActive(true);
                Debug.Log("WoldUnloader: Charge complete. UI enabled.");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(bunkerTag)) return;
        insideBunker = false;
        // Reset charge when leaving the bunker
        charge = 0f;
        UpdateChargeUI();
        if (UIPanel != null && UIPanel.activeSelf)
        {
            UIPanel.SetActive(false);
            Debug.Log("WoldUnloader: Auto-closed panel on exit.");
        }
        // Reset UI state so it can show again when re-entering after recharge
        uiShown = false;
        reopenAtTime = -1f;
    }

    void UpdateChargeUI()
    {
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
        if (insideBunker && uiShown)
        {
            reopenAtTime = Time.time + manualCloseDuration;
            Debug.Log("WoldUnloader: Panel closed temporarily for " + manualCloseDuration + "s.");
        }
        else
        {
            reopenAtTime = -1f;
        }
    }

    // Optional Button: immediately close with no timed reopen
    public void ClosePanelNow()
    {
        if (UIPanel == null) return;
        UIPanel.SetActive(false);
        reopenAtTime = -1f;
        Debug.Log("WoldUnloader: Panel closed.");
    }
}