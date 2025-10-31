using UnityEngine;

public class AudioPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    
    private void Awake()
    {
        // Get the AudioSource component if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Disable play on awake since we want to control it manually
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }
    
    private void Update()
    {
        // Check for spacebar press using Unity's legacy input system
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayAudio();
        }
    }
    
    private void PlayAudio()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            // Stop any currently playing audio and play the clip
            audioSource.Stop();
            audioSource.Play();
            Debug.Log("Playing audio: " + audioSource.clip.name);
        }
        else
        {
            Debug.LogWarning("AudioSource or AudioClip is missing on " + gameObject.name);
        }
    }
    
    // Public method to play audio (can be called from other scripts or UI)
    public void PlayAudioClip()
    {
        PlayAudio();
    }
}