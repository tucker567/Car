using UnityEngine;

public class AmbiantSounds : MonoBehaviour
{
    public AudioClip loopClip; // Assign your sound effect in the Inspector
    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = loopClip;
        audioSource.loop = true;
        audioSource.volume = 1.5f; // Set volume above default max (will amplify)
        audioSource.Play();
    }
}
