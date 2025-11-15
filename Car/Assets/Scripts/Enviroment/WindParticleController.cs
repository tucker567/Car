using UnityEngine;

public class WindParticleController : MonoBehaviour
{
    public ParticleSystem customParticleSystem;
    public Vector3 windDirection = new Vector3(5f, 0f, 0f); // Example: wind blowing right
    public Transform playerTransform; // Optional manual assignment

    [Header("Player Auto-Find")]
    public bool autoFindPlayer = true;
    public string playerTag = "playerCar";
    public float playerSearchInterval = 0.5f; // seconds between searches

    private bool _playerSearchStarted = false;
    private float _nextPlayerSearchTime = 0f;

    void Start()
    {
        var main = customParticleSystem.main;

        var forceOverLifetime = customParticleSystem.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.x = windDirection.x;
        forceOverLifetime.y = windDirection.y;
        forceOverLifetime.z = windDirection.z;
    }

    void LateUpdate()
    {
        if (playerTransform != null)
        {
            // Move to player's position, keep current rotation
            transform.position = playerTransform.position;
            // Do NOT set transform.rotation, so rotation stays unchanged
        }
    }

    void Update()
    {
        // Late player spawn support
        if (playerTransform == null && autoFindPlayer)
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
                    playerTransform = tagged.transform;
                    Debug.Log("WindParticleController: Attached to player '" + playerTransform.name + "' via tag '" + playerTag + "'.");
                }
                _nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);
            }
        }
    }
}