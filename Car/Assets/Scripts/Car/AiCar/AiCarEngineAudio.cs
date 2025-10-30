using UnityEngine;

public class AiCarEngineAudio : MonoBehaviour
{
    public AudioSource engineAudio;
    public float minPitch = 0.8f;
    public float maxPitch = 2.0f;
    public float idleVolume = 0.15f; // Louder idle
    public float maxVolume = 0.3f;   // Volume at max speed
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
            engineAudio.volume = idleVolume;
            engineAudio.spatialBlend = 1f; // Make audio fully 3D
            engineAudio.Play();
        }
    }

    void Update()
    {
        if (engineAudio != null && rigid != null)
        {
            float speed = rigid.linearVelocity.magnitude;
            engineAudio.pitch = Mathf.Lerp(minPitch, maxPitch, speed / 40f);
            engineAudio.volume = Mathf.Lerp(idleVolume, maxVolume, speed / 40f);
        }
    }
}