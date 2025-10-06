using UnityEngine;

public class AmbiantSounds : MonoBehaviour
{
    public AudioClip loopClip;         // Main wind sound
    public AudioClip strongWindClip;   // Stronger wind sound
    public AudioClip variationWindClip; // Additional wind variation

    private AudioSource mainWindSource;
    private AudioSource strongWindSource;
    private AudioSource variationWindSource;

    // For random gusts
    public float minGustInterval = 4f;
    public float maxGustInterval = 12f;
    public float gustDuration = 2.5f;

    void Start()
    {
        // Main wind
        mainWindSource = gameObject.AddComponent<AudioSource>();
        mainWindSource.clip = loopClip;
        mainWindSource.loop = true;
        mainWindSource.volume = 1.2f;
        mainWindSource.Play();

        // Stronger wind (gusts)
        if (strongWindClip != null)
        {
            strongWindSource = gameObject.AddComponent<AudioSource>();
            strongWindSource.clip = strongWindClip;
            strongWindSource.loop = true;
            strongWindSource.volume = 0f; // Start silent, fade in for gusts
            strongWindSource.Play();
            StartCoroutine(RandomGusts());
        }

        // Variation wind (always low in background)
        if (variationWindClip != null)
        {
            variationWindSource = gameObject.AddComponent<AudioSource>();
            variationWindSource.clip = variationWindClip;
            variationWindSource.loop = true;
            variationWindSource.volume = 0.4f;
            variationWindSource.Play();
        }
    }

    System.Collections.IEnumerator RandomGusts()
    {
        while (true)
        {
            float waitTime = Random.Range(minGustInterval, maxGustInterval);
            yield return new WaitForSeconds(waitTime);

            // Fade in gust
            float t = 0f;
            while (t < gustDuration)
            {
                t += Time.deltaTime;
                strongWindSource.volume = Mathf.Lerp(0f, 0.8f, t / gustDuration);
                yield return null;
            }

            // Hold gust
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));

            // Fade out gust
            t = 0f;
            while (t < gustDuration)
            {
                t += Time.deltaTime;
                strongWindSource.volume = Mathf.Lerp(0.8f, 0f, t / gustDuration);
                yield return null;
            }
            strongWindSource.volume = 0f;
        }
    }
}
