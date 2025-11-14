using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class Playlist
{
    public string albumName;
    public List<AudioClip> songs;
}

public class Radio : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;
    public List<Playlist> playlists;
    [Range(0f, 1.5f)]
    public float volume = 1f; // Volume slider in Inspector

    [Header("UI")]
    public TMP_Text albumText;
    public TMP_Text songText;

    private int currentPlaylist = 0;
    private int currentSong = 0;
    private bool isPlaying = false;

    private List<int> shuffledOrder = new List<int>();

    void Awake()
    {
        // albumText
        if (albumText == null)
        {
            albumText = GameObject.Find("Canvas/AlbumText")?.GetComponent<TMP_Text>();
            if (albumText == null) albumText = GameObject.Find("AlbumText")?.GetComponent<TMP_Text>();
            if (albumText == null)
            {
                GameObject byTag = null;
                try { byTag = GameObject.FindGameObjectWithTag("AlbumText"); } catch { }
                if (byTag != null) albumText = byTag.GetComponent<TMP_Text>();
            }
            if (albumText == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (var t in all) { if (t != null && t.name == "AlbumText") { albumText = t; break; } }
            }
            if (albumText == null)
                Debug.LogWarning("[Radio] albumText not found. Assign, name, or tag it 'AlbumText'.");
        }

        // songText
        if (songText == null)
        {
            songText = GameObject.Find("Canvas/SongText")?.GetComponent<TMP_Text>();
            if (songText == null) songText = GameObject.Find("SongText")?.GetComponent<TMP_Text>();
            if (songText == null)
            {
                GameObject byTag = null;
                try { byTag = GameObject.FindGameObjectWithTag("SongText"); } catch { }
                if (byTag != null) songText = byTag.GetComponent<TMP_Text>();
            }
            if (songText == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (var t in all) { if (t != null && t.name == "SongText") { songText = t; break; } }
            }
            if (songText == null)
                Debug.LogWarning("[Radio] songText not found. Assign, name, or tag it 'SongText'.");
        }
    }

    void Start()
    {
        if (audioSource != null)
            audioSource.volume = volume;

        if (playlists.Count > 0 && playlists[0].songs.Count > 0)
        {
            ShuffleSongs();
            PlaySong();
        }
    }

    void Update()
    {
        if (audioSource != null)
            audioSource.volume = volume;

        // Automatically play next song when current finishes
        if (isPlaying && !audioSource.isPlaying && audioSource.clip != null)
        {
            NextSong();
        }

        if (Keyboard.current == null) return;

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            NextSong();
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            PreviousSong();
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            TogglePlayPause();
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            NextPlaylist();
    }

    void ShuffleSongs()
    {
        shuffledOrder.Clear();
        int songCount = playlists[currentPlaylist].songs.Count;
        for (int i = 0; i < songCount; i++)
            shuffledOrder.Add(i);

        // Fisher-Yates shuffle
        for (int i = songCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = shuffledOrder[i];
            shuffledOrder[i] = shuffledOrder[j];
            shuffledOrder[j] = temp;
        }
        currentSong = 0;
    }

    void PlaySong()
    {
        if (playlists.Count == 0 || playlists[currentPlaylist].songs.Count == 0) return;
        StartCoroutine(PlaySongAsync(playlists[currentPlaylist].songs[shuffledOrder[currentSong]]));
        UpdateUI();
    }

    System.Collections.IEnumerator PlaySongAsync(AudioClip clip)
    {
        if (!clip.loadState.Equals(AudioDataLoadState.Loaded))
            clip.LoadAudioData();

        // Wait until loaded
        while (clip.loadState == AudioDataLoadState.Loading)
            yield return null;

        audioSource.clip = clip;
        audioSource.Play();
        isPlaying = true;
    }

    void NextSong()
    {
        var songs = playlists[currentPlaylist].songs;
        currentSong = (currentSong + 1) % songs.Count;
        PlaySong();
    }

    void PreviousSong()
    {
        var songs = playlists[currentPlaylist].songs;
        currentSong = (currentSong - 1 + songs.Count) % songs.Count;
        PlaySong();
    }

    void TogglePlayPause()
    {
        if (isPlaying)
        {
            audioSource.Pause();
            isPlaying = false;
        }
        else
        {
            audioSource.Play();
            isPlaying = true;
        }
    }

    void NextPlaylist()
    {
        currentPlaylist = (currentPlaylist + 1) % playlists.Count;
        ShuffleSongs();
        PlaySong();
    }

    void UpdateUI()
    {
        // Album/playlist name
        if (albumText != null)
            albumText.text = playlists[currentPlaylist].albumName;

        // Song name (AudioClip name)
        if (songText != null)
        {
            var songs = playlists[currentPlaylist].songs;
            if (songs.Count > 0)
                songText.text = songs[shuffledOrder[currentSong]].name;
            else
                songText.text = "";
        }
    }
}
