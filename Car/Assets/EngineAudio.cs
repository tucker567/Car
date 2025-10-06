using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Add this for the new Input System

public class Engine : MonoBehaviour
{
    AudioSource audioSource;
    private float targetPitch;
    public float pitchChangeSpeed = 1.5f; // How fast pitch changes
    public float MaxPitch = 2.5f;
    public float MinPitch = 0.7f;
    public float IdlePitch = 1.0f; // Pitch when not accelerating

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.Play();
            audioSource.pitch = IdlePitch;
            targetPitch = IdlePitch;
        }
    }

    void Update()
    {
        // Use the new Input System
        if (Keyboard.current != null && Keyboard.current.wKey.isPressed)
        {
            targetPitch += pitchChangeSpeed * Time.deltaTime;
        }
        else
        {
            targetPitch -= pitchChangeSpeed * Time.deltaTime;
        }

        // Clamp pitch between MinPitch and MaxPitch
        targetPitch = Mathf.Clamp(targetPitch, MinPitch, MaxPitch);

        // Smoothly apply pitch to audio source
        audioSource.pitch = Mathf.Lerp(audioSource.pitch, targetPitch, 0.2f);
    }
}

