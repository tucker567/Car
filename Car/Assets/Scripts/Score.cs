using UnityEngine;
using System.Collections;

public class Score : MonoBehaviour
{
    [Header("Scoring Control")]
    public bool scoringEnabled = true;
    public static int currentScore = 0;
    [Header("Time Score")]
    public int Timealive;
    public int TimealiveMultiplier = 10;
    public int TimealiveTotalScore;
    [Header("Distance Score")]
    public int DistanceTravelled;
    public int DistanceTravelledMultiplier = 5;
    public int DistanceTravelledTotalScore;
    [Header("Quest Score")]
    [Tooltip("Total bonus accumulated from completed quests.")]
    public int questBonusAccum = 0;
    [Tooltip("Default points awarded per completed quest if no amount is provided.")]
    public int questCompletionBonusDefault = 1000;
    // Quests disabled per request; kept for reference but unused
    // public int QuestsCompleted;
    // public int QuestsCompletedMultiplier = 100;
    [Header("UI Elements")]
    public TMPro.TMP_Text scoreText;
    public GameObject smallblackhomebutton;
    [Header("Display Settings")]
    public int displayedScore;
    public float countUpSpeed = 250f; // points per second
    public float countUpMultiplier = 1.2f; // speed multiplier for count-up animation
    [SerializeField] private float countUpDelaySeconds = 5f; // delay before count-up starts after stopping
    [Header("Player Settings")]
    public string playerTag = "playerCar";
    public Transform player;
    public bool autoFindPlayer;
    public TMPro.TMP_Text highScoreText;

    // Runtime tracking
    private Vector3 _lastPosition;
    private bool _hasLastPosition;
    private float _distanceAccum; // meters
    private float _timeAccum;     // seconds while scoring
    private float _initialCountUpSpeed; // stored to allow reliable resets

    // High score persistence
    public static int highScore = 0;
    private const string HighScoreKey = "HighScore";

    // Count-up coroutine handle
    private Coroutine _countUpCoroutine;


    void Start()
    {
        currentScore = 0;
        _distanceAccum = 0f;
        _timeAccum = 0f;
        _hasLastPosition = false;
        _initialCountUpSpeed = countUpSpeed;

        // Load high score
        highScore = GetHighScore();
        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + highScore.ToString();
        }
    }

    void Update()
    {
        // Update timer only while scoring
        if (scoringEnabled)
        {
            _timeAccum += Time.deltaTime;
            Timealive = Mathf.FloorToInt(_timeAccum);
        }

        // Late player spawn support
        if (player == null && autoFindPlayer)
        {
            var tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null)
            {
                player = tagged.transform;
                // Initialize last position to avoid spike in distance when attaching
                _lastPosition = player.position;
                _hasLastPosition = true;
                Debug.Log("Score: Attached to player '" + player.name + "' via tag '" + playerTag + "'.");
            }
        }

        // Update distance travelled (accumulate real movement), only while scoring
        if (player != null && scoringEnabled)
        {
            if (!_hasLastPosition)
            {
                _lastPosition = player.position;
                _hasLastPosition = true;
            }
            else
            {
                float delta = Vector3.Distance(player.position, _lastPosition);
                _distanceAccum += delta;
                _lastPosition = player.position;
                DistanceTravelled = Mathf.FloorToInt(_distanceAccum);
            }
        }

        // Recalculate score and update UI every frame
        CalculateScore();
        UpdateHighScoreIfNeeded();

        // No automatic count-up here; it runs after stopping via coroutine
    }

    public void CalculateScore()
    {
        currentScore = (Timealive * TimealiveMultiplier) +
                       (DistanceTravelled * DistanceTravelledMultiplier) +
                       (questBonusAccum);
        if (scoreText != null)
        {
            scoreText.text = "Score: " + displayedScore.ToString();
        }
    }

    private void UpdateHighScoreIfNeeded()
    {
        if (SubmitHighScore(currentScore))
        {
            // Persist the new high score but do not update the label here
            // to avoid spoiling that a new high score was achieved.
            highScore = GetHighScore();
        }
    }

    // Public controls
    public void StartScoring()
    {
        scoringEnabled = true;
        // Reset last position to avoid distance spike after pause
        if (player != null)
        {
            _lastPosition = player.position;
            _hasLastPosition = true;
        }
    }

    public void StopScoring()
    {
        scoringEnabled = false;
        PlayerPrefs.Save();

        // Cancel any existing count-up and start a new delayed count-up
        if (_countUpCoroutine != null)
        {
            StopCoroutine(_countUpCoroutine);
            _countUpCoroutine = null;
        }
        _countUpCoroutine = StartCoroutine(CountUpAfterDelay());
        
        // Ensure that the score ui is active and visible
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            highScoreText.gameObject.SetActive(true);
        }
        
        if (smallblackhomebutton != null)
        {
            smallblackhomebutton.SetActive(false);
        }
    }

    public void ResetScore(bool resetHighScore = false)
    {
        currentScore = 0;
        _distanceAccum = 0f;
        _timeAccum = 0f;
        Timealive = 0;
        DistanceTravelled = 0;
        questBonusAccum = 0;
        _hasLastPosition = false;
        CalculateScore();
        displayedScore = currentScore;
        if (resetHighScore)
        {
            ResetHighScore();
            highScore = 0;
            if (highScoreText != null)
            {
                highScoreText.text = "High Score: 0";
            }
        }
    }

    // Reset only the displayed score and count-up speed (does not affect actual accumulated score)
    public void ResetDisplayedScoreAndSpeed()
    {
        // Stop any ongoing count-up animation
        if (_countUpCoroutine != null)
        {
            StopCoroutine(_countUpCoroutine);
            _countUpCoroutine = null;
        }

        // Reset the displayed score and restore count-up speed to its initial value
        displayedScore = 0;
        countUpSpeed = _initialCountUpSpeed;

        // Refresh the UI immediately
        if (scoreText != null)
        {
            scoreText.text = "Score: " + displayedScore.ToString();
        }
    }

    // Reset all round-related score state while preserving the saved high score
    public void ResetForNewRound()
    {
        // Stop any ongoing count-up animation
        if (_countUpCoroutine != null)
        {
            StopCoroutine(_countUpCoroutine);
            _countUpCoroutine = null;
        }

        // Reset live scoring state (but do not clear saved high score)
        ResetScore(resetHighScore: false);

        // Ensure displayed score starts at zero and count-up speed is restored
        displayedScore = 0;
        countUpSpeed = _initialCountUpSpeed;

        // Rebind last position to avoid distance spike on next movement
        if (player != null)
        {
            _lastPosition = player.position;
            _hasLastPosition = true;
        }

        // Refresh labels
        if (scoreText != null)
        {
            scoreText.text = "Score: 0";
        }
        RefreshHighScoreLabel();
    }

    // Convenience: reset state and begin scoring for a new round
    public void StartNewRound()
    {
        ResetForNewRound();
        StartScoring();
    }

    // Award quest completion points and refresh UI immediately if scoring is active
    public void AddQuestBonus(int points = -1)
    {
        int add = points < 0 ? questCompletionBonusDefault : points;
        if (add <= 0) return;
        questBonusAccum += add;
        CalculateScore();
        if (scoringEnabled)
        {
            displayedScore = currentScore;
            if (scoreText != null)
            {
                scoreText.text = "Score: " + displayedScore.ToString();
            }
        }
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
        _lastPosition = player != null ? player.position : Vector3.zero;
        _hasLastPosition = player != null;
    }

    private IEnumerator CountUpAfterDelay()
    {
        // Use realtime so it works even if timeScale is 0 (menus)
        yield return new WaitForSecondsRealtime(countUpDelaySeconds);

        // Animate displayedScore up to currentScore
        while (displayedScore < currentScore)
        {
            int step = Mathf.CeilToInt(countUpSpeed * Time.unscaledDeltaTime);
            displayedScore = Mathf.Min(currentScore, displayedScore + step);
            // Let Update() refresh UI via CalculateScore()
            // add countUpMultiplier effect
            countUpSpeed *= countUpMultiplier;


            yield return null;
        }
        displayedScore = currentScore;
        _countUpCoroutine = null;
    }

    // --- High Score API (moved from separate manager) ---
    public static int GetHighScore()
    {
        return PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    // Returns true if a new high score was set
    public static bool SubmitHighScore(int score)
    {
        int current = GetHighScore();
        if (score > current)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
            return true;
        }
        return false;
    }

    public static void ResetHighScore()
    {
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
    }

    // --- Editor convenience (shows in component context menu) ---
    [ContextMenu("Reset High Score")] 
    private void EditorResetHighScore()
    {
        ResetHighScore();
        highScore = 0;
        if (highScoreText != null)
        {
            highScoreText.text = "High Score: 0";
        }
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog("High Score", "High score has been reset.", "OK");
        #endif
    }

    [ContextMenu("Refresh High Score Label")] 
    private void EditorRefreshHighScore()
    {
        highScore = GetHighScore();
        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + highScore.ToString();
        }
    }

    // Public method to reveal the saved high score on UI (e.g., when entering menu)
    public void RefreshHighScoreLabel()
    {
        highScore = GetHighScore();
        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + highScore.ToString();
        }
    }
}
