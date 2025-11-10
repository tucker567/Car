using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingPanelController : MonoBehaviour
{
    [Header("Scenes")]
    public string worldSceneName = "World";

    [Header("Loading UI")]
    public TMPro.TextMeshProUGUI noteText;
    public GameObject panelRoot;
    public bool hidePanelWhenDone = true;

    [Header("Menu Cleanup")]
    public bool disableMenuRootWhenDone = true;
    public GameObject menuRoot;
    public bool unloadMenuSceneWhenDone = true;

    [Header("Gameplay UI Activation (World Scene)")]
    [Tooltip("Delay (seconds) after generation completes before enabling gameplay UI.")]
    public float gameplayUIActivateDelay = 0f;

    [Tooltip("If true, find all Canvas components in the world scene (top-level) and treat them as gameplay UI roots (excluding this loading panel if additively moved).")]
    public bool autoFindWorldCanvases = true;

    [Tooltip("Tag to search for in world scene for UI roots. Leave empty to skip.")]
    public string gameplayUITag = "";          // e.g. "GameplayUI"

    [Tooltip("Explicit GameObject names in the world scene to enable (exact match).")]
    public string[] gameplayUIObjectNames;      // e.g. ["HUDRoot", "MinimapRoot"]

    [Tooltip("Optional: manually assign roots if you load them elsewhere (will be merged with discovered ones).")]
    public GameObject[] additionalGameplayUIRoots;

    // Internal collected UI roots from world scene
    List<GameObject> _gameplayUIRoots = new List<GameObject>();

    bool started;
    WorldGenerator worldGen;
    Scene menuSceneRef;

    void Start()
    {
        // Scene this loader lives in (menu/title)
        menuSceneRef = gameObject.scene;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void BeginLoad()
    {
        if (started) return;
        started = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        StartCoroutine(RunLoadFlow());
    }

    IEnumerator RunLoadFlow()
    {
        SetNote("Loading world scene...");

        AsyncOperation load = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
        while (!load.isDone) yield return null;

        var worldScene = SceneManager.GetSceneByName(worldSceneName);
        if (worldScene.IsValid())
            SceneManager.SetActiveScene(worldScene);

        // Discover world generator
        foreach (var root in worldScene.GetRootGameObjects())
        {
            worldGen = root.GetComponentInChildren<WorldGenerator>(true);
            if (worldGen != null) break;
        }

        if (worldGen == null)
        {
            SetNote("WorldGenerator not found!");
            yield break;
        }

        // Collect gameplay UI roots now that the world scene is loaded
        CollectGameplayUIRoots(worldScene);

        // Make sure they start disabled
        foreach (var go in _gameplayUIRoots)
            if (go != null) go.SetActive(false);

        // Configure async generation explicitly
        worldGen.autoGenerateAtStart = false;
        worldGen.generateAsync = true;

        // Subscribe to notes
        worldGen.OnNote += SetNote;
        worldGen.OnGenerationComplete += OnWorldReady;

        // Run generation
        yield return worldGen.StartCoroutine(worldGen.GenerateWorldAsync());
        // Completion handled in OnWorldReady
    }

    void CollectGameplayUIRoots(Scene worldScene)
    {
        _gameplayUIRoots.Clear();

        // 1. Tag-based
        if (!string.IsNullOrEmpty(gameplayUITag))
        {
            // FindWithTag only searches active objects; use scene roots enumeration
            foreach (var root in worldScene.GetRootGameObjects())
            {
                var tagged = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in tagged)
                {
                    if (t.gameObject.CompareTag(gameplayUITag))
                        _gameplayUIRoots.Add(t.gameObject);
                }
            }
        }

        // 2. Name-based
        if (gameplayUIObjectNames != null && gameplayUIObjectNames.Length > 0)
        {
            HashSet<string> nameSet = new HashSet<string>(gameplayUIObjectNames);
            foreach (var root in worldScene.GetRootGameObjects())
            {
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (nameSet.Contains(t.gameObject.name))
                        _gameplayUIRoots.Add(t.gameObject);
                }
            }
        }

        // 3. Auto-find canvases
        if (autoFindWorldCanvases)
        {
            foreach (var root in worldScene.GetRootGameObjects())
            {
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (var c in canvases)
                {
                    // Skip panelRoot if it ended up moved into world scene somehow
                    if (panelRoot != null && c.gameObject == panelRoot) continue;
                    _gameplayUIRoots.Add(c.gameObject);
                }
            }
        }

        // 4. Merge manually assigned
        if (additionalGameplayUIRoots != null)
        {
            foreach (var go in additionalGameplayUIRoots)
                if (go != null) _gameplayUIRoots.Add(go);
        }

        // 5. Deduplicate
        for (int i = _gameplayUIRoots.Count - 1; i >= 0; i--)
        {
            if (_gameplayUIRoots[i] == null) _gameplayUIRoots.RemoveAt(i);
        }
        var unique = new HashSet<GameObject>(_gameplayUIRoots);
        _gameplayUIRoots = new List<GameObject>(unique);
    }

    void SetNote(string msg)
    {
        if (noteText != null) noteText.text = msg;
        Debug.Log("[Load] " + msg);
    }

    void OnWorldReady()
    {
        SetNote("World ready!");

        worldGen.OnNote -= SetNote;
        worldGen.OnGenerationComplete -= OnWorldReady;

        // Hide loading panel
        if (hidePanelWhenDone && panelRoot != null)
        {
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg != null)
                StartCoroutine(FadeAndHide(cg, 0.35f));
            else
                panelRoot.SetActive(false);
        }

        // Disable menu UI
        if (disableMenuRootWhenDone && menuRoot != null)
            menuRoot.SetActive(false);

        // Activate gameplay UI after optional delay
        if (gameplayUIActivateDelay > 0f)
            StartCoroutine(EnableGameplayUIAfterDelay(gameplayUIActivateDelay));
        else
            EnableGameplayUINow();

        // Unload menu scene if requested
        if (unloadMenuSceneWhenDone &&
            menuSceneRef.IsValid() &&
            menuSceneRef.isLoaded &&
            menuSceneRef != SceneManager.GetActiveScene())
        {
            SceneManager.UnloadSceneAsync(menuSceneRef);
        }
    }

    IEnumerator EnableGameplayUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        EnableGameplayUINow();
    }

    void EnableGameplayUINow()
    {
        foreach (var go in _gameplayUIRoots)
            if (go != null) go.SetActive(true);
    }

    IEnumerator FadeAndHide(CanvasGroup cg, float duration)
    {
        float t = 0f;
        float start = cg.alpha;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, t / Mathf.Max(0.0001f, duration));
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }
}
