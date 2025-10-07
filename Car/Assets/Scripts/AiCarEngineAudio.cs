using UnityEngine;

public class AiCarEngineAudio : MonoBehaviour
{
    public AudioSource engineAudio;
    public float minPitch = 0.8f;
    public float maxPitch = 2.0f;
    public float constantVolume = 0.2f; // Set a lower constant volume
    public Rigidbody rigid;

    void Start()
    {
        if (engineAudio == null)
            engineAudio = GetComponent<AudioSource>();
        if (rigid == null)
            rigid = GetComponent<Rigidbody>();
        if (engineAudio != null)
        {
            engineAudio.loop = true;
            engineAudio.volume = constantVolume; // Set volume once
            engineAudio.Play();
        }
    }

    void Update()
    {
        if (engineAudio != null && rigid != null)
        {
            float speed = rigid.linearVelocity.magnitude;
            engineAudio.pitch = Mathf.Lerp(minPitch, maxPitch, speed / 40f);
            // Volume stays constant
        }
    }
}