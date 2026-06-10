using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using Unity.InferenceEngine.Samples.TTS.Inference;
using Unity.Jobs;

[RequireComponent(typeof(AudioSource))]
public class KokoroManager : MonoBehaviour
{
    [Header("Sentis Settings")]
    public BackendType backendType = BackendType.CPU;
    
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
        Debug.Log($"[KokoroManager] Initializing KokoroManager with backend: {backendType}");
        
        // GPUCompute (DirectML) is causing a hard D3D12 crash on this laptop's architecture for this ONNX model.
        // Forcing CPU backend universally, which is highly stable and extremely fast due to Burst compilation.
        Debug.LogWarning("[KokoroManager] Forcing BackendType.CPU to prevent DML driver crashes.");
        backendType = BackendType.CPU;

        // Initialize the local Sentis handler with lazyLoadModel=false so that the model and worker
        // are fully loaded and prepared on the main thread before any background threads call them.
        kokoroHandler = new KokoroHandler(backendType, lazyLoadModel: false);
        
        // Warm up MisakiSharp phonetic dictionaries on the main thread so they are loaded and ready
        // for background-thread tokenization.
        Debug.Log("[KokoroManager] Warming up MisakiSharp phonetic dictionaries on the main thread...");
        MisakiSharp.TokenizeGraphemes("warmup");
        
        // Load all voices from Resources/Voices/
        Debug.Log("[KokoroManager] Loading available voices...");
        availableVoices = KokoroHandler.GetVoices();
        if (availableVoices == null || availableVoices.Count == 0)
        {
            Debug.LogError("[KokoroManager] FAILED TO LOAD ANY VOICES from Resources/Voices/. Check voicesIndex.txt and .bin assets.");
        }
        else
        {
            Debug.Log($"[KokoroManager] Successfully loaded {availableVoices.Count} voices.");
        }
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

    public void StopSpeech()
    {
        if (targetAudioSource != null && targetAudioSource.isPlaying)
        {
            targetAudioSource.Stop();
        }
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Note: The async Task.Delay in GenerateAndPlayLocal will still finish,
        // but since the audio is stopped, it's effectively silent.
        // We could use a CancellationToken if we needed more robust cancellation.
    }

    public void SynthesizeAndPlay(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // Use a Task-based synthesis flow
        _ = GenerateAndPlayLocal(text);
    }

    private class KokoroJobData
    {
        public KokoroHandler handler;
        public int[] tokens;
        public float speed;
        public KokoroHandler.Voice voice;
        public float[] result;
    }

    private static readonly List<KokoroJobData> activeJobs = new List<KokoroJobData>();
    private static readonly object jobLock = new object();

    private struct KokoroInferenceJob : Unity.Jobs.IJob
    {
        public int jobIndex;

        public void Execute()
        {
            KokoroJobData data;
            lock (jobLock)
            {
                data = activeJobs[jobIndex];
            }

            try
            {
                data.result = data.handler.ExecuteAndExtract(data.tokens, data.speed, data.voice);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KokoroJob] Inference error on worker thread: {ex.Message}");
            }
        }
    }

    public async Task<AudioClip> Synthesize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (activeVoice == null)
        {
            Debug.LogError("No Kokoro voice selected!");
            return null;
        }

        // Capture voice and speed on main thread to prevent thread-safety issues
        var voiceToUse = activeVoice;
        float currentSpeed = speed;

        try
        {
            float ttsStart = Time.realtimeSinceStartup;
            Debug.Log($"[PERF] [{DateTime.Now:HH:mm:ss.fff}] Starting TTS Synthesis...");

            // 1. Text tokenization in the background
            List<int[]> sentenceTokens = await Task.Run(() =>
            {
                string[] sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                List<int[]> tokensList = new List<int[]>();

                foreach (string sentence in sentences)
                {
                    string trimmed = sentence.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Add punctuation back for better prosody
                    char punctuation = text.Length > text.IndexOf(sentence) + sentence.Length ? text[text.IndexOf(sentence) + sentence.Length] : '.';
                    trimmed += punctuation;

                    int[] tokens = MisakiSharp.TokenizeGraphemes(trimmed);
                    if (tokens.Length > 500)
                    {
                        Debug.LogWarning($"Sentence too long ({tokens.Length} tokens), skipping chunk.");
                        continue;
                    }
                    tokensList.Add(tokens);
                }
                return tokensList;
            });

            if (sentenceTokens == null || sentenceTokens.Count == 0) return null;

            // 2. Schedule model execution on Unity Job System's worker threads!
            // Each sentence runs on a separate worker thread, completely in parallel and off the main thread.
            // Under Unity, Temp native allocations are fully supported inside Job System worker threads.
            var handles = new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(sentenceTokens.Count, Unity.Collections.Allocator.TempJob);
            List<KokoroJobData> jobsData = new List<KokoroJobData>();

            lock (jobLock)
            {
                for (int i = 0; i < sentenceTokens.Count; i++)
                {
                    var data = new KokoroJobData
                    {
                        handler = kokoroHandler,
                        tokens = sentenceTokens[i],
                        speed = currentSpeed,
                        voice = voiceToUse
                    };
                    jobsData.Add(data);
                    activeJobs.Add(data);
                    int index = activeJobs.Count - 1;

                    var job = new KokoroInferenceJob { jobIndex = index };
                    handles[i] = job.Schedule();
                }
            }

            // Combine all job handles to wait for them together
            var combinedHandle = Unity.Jobs.JobHandle.CombineDependencies(handles);
            handles.Dispose();

            // Await completion of all jobs while yielding control to the main thread!
            // This ensures 100% smooth, lag-free gameplay while synthesis is running.
            while (!combinedHandle.IsCompleted)
            {
                await Task.Yield();
            }
            combinedHandle.Complete(); // finalize/clean up the jobs

            // 3. Retrieve results and clean up activeJobs list
            List<float[]> audioChunks = new List<float[]>();
            int totalLength = 0;

            lock (jobLock)
            {
                foreach (var data in jobsData)
                {
                    if (data.result != null && data.result.Length > 0)
                    {
                        audioChunks.Add(data.result);
                        totalLength += data.result.Length;
                    }
                    activeJobs.Remove(data);
                }
            }

            if (totalLength == 0) return null;

            // 4. Merge Chunks
            float[] mergedData = new float[totalLength];
            int offset = 0;
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, mergedData, offset, chunk.Length);
                offset += chunk.Length;
            }

            // 5. Create final AudioClip (must be on the main thread!)
            AudioClip clip = AudioClip.Create("Kokoro_Briefing", mergedData.Length, 1, 24000, false);
            clip.SetData(mergedData, 0);

            float ttsDuration = Time.realtimeSinceStartup - ttsStart;
            Debug.Log($"Generation Debug: TTS generated (ready to play): {ttsDuration:F1}s");
            Debug.Log($"[PERF] [{DateTime.Now:HH:mm:ss.fff}] TTS Synthesis Finished. Ready to Play.");
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError($"Sentis Kokoro Synthesis Error: {e.Message}");
            return null;
        }
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
            AudioClip clip = await Synthesize(text);
            if (clip == null) return;

            // 4. Play
            AudioSource currentSource = targetAudioSource != null ? targetAudioSource : audioSource;
            currentSource.clip = clip;
            currentSource.Play();

            // Wait for audio to finish before invoking the event
            float duration = clip.length;
            await Task.Delay((int)(duration * 1000));
            
            Debug.Log($"[PERF] [{DateTime.Now:HH:mm:ss.fff}] Voice Generation & Playback Done.");
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
