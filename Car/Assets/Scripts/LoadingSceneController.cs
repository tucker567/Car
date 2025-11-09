using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingPanelController : MonoBehaviour
{
    [Header("Scenes")]
    public string worldSceneName = "World";

    [Header("References")]
    [Tooltip("Text component to display generation notes.")]
    public TMPro.TextMeshProUGUI noteText;

    [Header("Panel Behavior")]
    public GameObject panelRoot;          // The panel GameObject you want to show/hide
    public bool hidePanelWhenDone = true;
    public bool disableMenuRootWhenDone = true;
    public GameObject menuRoot;           // (Optional) your menu buttons root

    [Header("Menu Scene Unload")]
    [Tooltip("If true, unload the menu/title scene after the world is generated.")]
    public bool unloadMenuSceneWhenDone = true;

    bool started = false;
    WorldGenerator worldGen;
    Scene menuSceneRef;

    void Start()
    {
        // Cache the scene this controller lives in (your title/menu scene)
        menuSceneRef = gameObject.scene;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // Call this from a UI Button (Play) or auto from Start if you prefer.
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

        // Load world additively so this panel stays visible
        AsyncOperation load = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
        while (!load.isDone) yield return null;

        var worldScene = SceneManager.GetSceneByName(worldSceneName);
        if (worldScene.IsValid())
            SceneManager.SetActiveScene(worldScene);

        // Locate WorldGenerator in the loaded world scene
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

        // Configure async generation
        worldGen.autoGenerateAtStart = false;
        worldGen.generateAsync = true;

        // Subscribe to progress/note events
        worldGen.OnNote += SetNote;
        worldGen.OnGenerationComplete += OnWorldReady;

        // Run generation coroutine
        yield return worldGen.StartCoroutine(worldGen.GenerateWorldAsync());
        // OnWorldReady will handle cleanup
    }

    void SetNote(string msg)
    {
        if (noteText != null) noteText.text = msg;
        Debug.Log("[Load] " + msg);
    }

    void OnWorldReady()
    {
        SetNote("World ready!");

        // Unsubscribe first (before potentially unloading this scene)
        worldGen.OnNote -= SetNote;
        worldGen.OnGenerationComplete -= OnWorldReady;

        if (hidePanelWhenDone && panelRoot != null)
            panelRoot.SetActive(false);

        if (disableMenuRootWhenDone && menuRoot != null)
            menuRoot.SetActive(false);

        // Unload the original menu/title scene if requested
        if (unloadMenuSceneWhenDone &&
            menuSceneRef.IsValid() &&
            menuSceneRef.isLoaded &&
            menuSceneRef != SceneManager.GetActiveScene())
        {
            SceneManager.UnloadSceneAsync(menuSceneRef);
        }
    }
}
