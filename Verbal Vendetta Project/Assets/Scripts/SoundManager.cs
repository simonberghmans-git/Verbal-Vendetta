using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header("UI Sound Settings")]
    public AudioClip typewriterKeySound;
    public AudioSource uiAudioSource;
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    public void PlayUISound()
    {
        if (typewriterKeySound != null && uiAudioSource != null)
        {
            // Vary the pitch slightly
            uiAudioSource.pitch = Random.Range(minPitch, maxPitch);
            uiAudioSource.PlayOneShot(typewriterKeySound);
        }
    }
}
