using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using Unity.InferenceEngine.Samples.TTS.Inference;

[RequireComponent(typeof(AudioSource))]
public class KokoroManager : MonoBehaviour
{
    [Header("Sentis Settings")]
    public BackendType backendType = BackendType.GPUCompute;
    
    [Header("Speech Settings")]
    [Range(0.5f, 2.0f)] public float speed = 1.0f;
    [Header("Test Mode")]
    public bool testing = false;
    
    private AudioSource audioSource;
    private AudioSource targetAudioSource; // Added for dynamic targeting
    private KokoroHandler kokoroHandler;
    private List<KokoroHandler.Voice> availableVoices;
    private KokoroHandler.Voice activeVoice;

    public event Action OnSpeechFinished;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // Initialize the local Sentis handler
        kokoroHandler = new KokoroHandler(backendType);
        
        // Load all voices from Resources/Voices/
        availableVoices = KokoroHandler.GetVoices();
    }

    public void SetVoice(string voiceName)
    {
        // Find the voice by filename (e.g., "af_heart.bin")
        activeVoice = availableVoices.Find(v => v.Name.Contains(voiceName));
        
        if (activeVoice == null && availableVoices.Count > 0)
        {
            activeVoice = availableVoices[0];
            Debug.LogWarning($"Voice '{voiceName}' not found. Defaulting to {activeVoice.Name}");
        }
    }

    public void SetTargetAudioSource(AudioSource source)
    {
        targetAudioSource = source;
    }

    public void SynthesizeAndPlay(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // Use a Task-based synthesis flow
        _ = GenerateAndPlayLocal(text);
    }

    private async Task GenerateAndPlayLocal(string text)
    {
        if (activeVoice == null)
        {
            Debug.LogError("No Kokoro voice selected!");
            return;
        }

        try
        {
            // 1. Convert text to phoneme IDs using MisakiSharp
            int[] tokens = MisakiSharp.TokenizeGraphemes(text);

            // 2. Run Sentis Inference
            using Tensor<float> outputTensor = await kokoroHandler.Execute(tokens, speed, activeVoice);

            // 3. Convert Tensor output to AudioClip
            float[] audioData = outputTensor.DownloadToArray();
            
            // Kokoro output is always 24000Hz mono
            AudioClip clip = AudioClip.Create("Kokoro_Speech", audioData.Length, 1, 24000, false);
            clip.SetData(audioData, 0);

            // 4. Play
            AudioSource currentSource = targetAudioSource != null ? targetAudioSource : audioSource;
            currentSource.clip = clip;
            currentSource.Play();

            // Wait for audio to finish before invoking the event
            float duration = clip.length;
            await Task.Delay((int)(duration * 1000));
            
            OnSpeechFinished?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"Sentis Kokoro Error: {e.Message}");
            OnSpeechFinished?.Invoke();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && testing)
        {
            testing = false; // Reset the toggle
            PlayTestLine();
        }
    }

    private void PlayTestLine()
    {
        if (availableVoices == null || availableVoices.Count == 0)
        {
            Debug.LogWarning("No voices loaded yet. Start the game first.");
            return;
        }
        
        // Pick a random voice
        var randomVoice = availableVoices[UnityEngine.Random.Range(0, availableVoices.Count)];
        activeVoice = randomVoice;
        
        Debug.Log($"[Kokoro Test] Speaking with voice: {randomVoice.Name}");
        SynthesizeAndPlay("This is a test of the local Sentis voice system. If you can hear this, the model is working correctly.");
    }

    private void OnDestroy()
    {
        kokoroHandler?.Dispose();
        if (availableVoices != null)
        {
            foreach (var v in availableVoices) v.Dispose();
        }
    }
}
