using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Newtonsoft.Json;

[RequireComponent(typeof(AudioSource))]
public class GeminiRemoteTTSManager : MonoBehaviour
{
    private const string API_KEY_FILE = "apikey.txt";

    [Header("Gemini TTS Settings")]
    public string geminiApiKey; // Re-added for direct loading
    [Range(0.5f, 2.0f)] public float speed = 1.0f;
    
    [Header("Test Mode")]
    public bool testing = false;

    private AudioSource audioSource;
    private AudioSource targetAudioSource; 
    private string activeVoice; 
    private GeminiRemoteTTSHandler geminiRemoteTTSHandler;


    public event Action OnSpeechFinished;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        Debug.Log($"[GeminiRemoteTTSManager] Initializing GeminiRemoteTTSManager.");

        LoadApiKey(); // Load API key directly

        if (string.IsNullOrEmpty(geminiApiKey))
        {
            Debug.LogError("[GeminiRemoteTTSManager] API Key is missing! Cannot initialize TTS handler.");
            return;
        }

        // Initialize the Gemini Remote TTS handler with the directly loaded API key
        geminiRemoteTTSHandler = new GeminiRemoteTTSHandler(geminiApiKey);
    }

    private void LoadApiKey()
    {
        // Load from project root (parent of Assets folder)
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string filePath = System.IO.Path.Combine(projectRoot, API_KEY_FILE);
        if (System.IO.File.Exists(filePath))
        {
            geminiApiKey = System.IO.File.ReadAllText(filePath).Trim();
            Debug.Log("[GeminiRemoteTTSManager] API key loaded from file.");
        }
        else
        {
            Debug.LogError($"[GeminiRemoteTTSManager] API key file not found at: {filePath}. Please create {API_KEY_FILE} in the project root.");
        }
    }

    public void SetVoice(string voiceName)
    {
        activeVoice = voiceName;
        Debug.Log($"[GeminiRemoteTTSManager] Active voice set to: {activeVoice}");
    }

    public void SetTargetAudioSource(AudioSource source)
    {
        targetAudioSource = source;
    }

    public void StopSpeech()
    {
        if (targetAudioSource != null && targetAudioSource.isPlaying) targetAudioSource.Stop();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
    }

    public void SynthesizeAndPlay(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _ = GenerateAndPlayRemote(text);
    }

    public async Task<AudioClip> Synthesize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (string.IsNullOrEmpty(activeVoice))
        {
            Debug.LogError("[GeminiRemoteTTSManager] No Gemini voice selected!");
            return null;
        }

        try
        {
            float ttsStart = Time.realtimeSinceStartup;
            Debug.Log($"[PERF] [{DateTime.Now:HH:mm:ss.fff}] Starting Gemini TTS Synthesis...");
            
            AudioClip clip = await geminiRemoteTTSHandler.Synthesize(text, activeVoice, speed);

            float ttsDuration = Time.realtimeSinceStartup - ttsStart;
            Debug.Log($"[GeminiRemoteTTSManager] TTS generated (ready to play): {ttsDuration:F1}s");
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GeminiRemoteTTSManager] Gemini TTS Synthesis Error: {e.Message}");
            return null;
        }
    }

    private async Task GenerateAndPlayRemote(string text)
    {
        if (string.IsNullOrEmpty(activeVoice))
        {
            Debug.LogError("[GeminiRemoteTTSManager] No Gemini voice selected!");
            return;
        }

        try
        {
            AudioClip clip = await Synthesize(text);
            if (clip == null) return;

            AudioSource currentSource = targetAudioSource != null ? targetAudioSource : audioSource;
            currentSource.clip = clip;
            currentSource.Play();

            float duration = clip.length;
            await Task.Delay((int)(duration * 1000));
            OnSpeechFinished?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GeminiRemoteTTSManager] Error playing Gemini TTS audio: {e.Message}");
        }
    }

    /// <summary>
    /// Handles communication with the remote European Google Cloud TTS service.
    /// </summary>
    public class GeminiRemoteTTSHandler
    {
        private string apiKey; // Re-added as a private field
        private const string GeminiTTSEndpoint = "https://eu-texttospeech.googleapis.com/v1/text:synthesize";

        public GeminiRemoteTTSHandler(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<AudioClip> Synthesize(string text, string voiceId, float speed)
        {
            string languageCode = "en-US";
            if (voiceId.StartsWith("en-GB")) languageCode = "en-GB";
            else if (voiceId.StartsWith("nl-BE")) languageCode = "nl-BE";
            else if (voiceId.StartsWith("fr-BE")) languageCode = "fr-BE";

            // Use direct API key authentication for a 'generateContent' style endpoint.
            string authenticatedUrl = $"{GeminiTTSEndpoint}?key={this.apiKey.Trim()}";

            var requestBody = new SynthesizeSpeechRequest
            {
                input = new SynthesisInput { text = text },
                voice = new VoiceSelectionParams { languageCode = languageCode, name = voiceId },
                audioConfig = new AudioConfig { audioEncoding = "LINEAR16", speakingRate = speed },
                model = "gemini-3.1-flash-tts-preview" // Specify the Gemini Flash TTS model
            };

            string jsonPayload = JsonConvert.SerializeObject(requestBody);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest www = new UnityWebRequest(authenticatedUrl, UnityWebRequest.kHttpVerbPOST))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();

                www.SetRequestHeader("Content-Type", "application/json");

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GeminiRemoteTTSHandler] Google Cloud TTS API Error: {www.error}\nDetails: {www.downloadHandler.text}");
                    return null;
                }

                try
                {
                    var responseData = JsonConvert.DeserializeObject<SynthesizeSpeechResponse>(www.downloadHandler.text);

                    if (responseData == null || string.IsNullOrEmpty(responseData.audioContent))
                    {
                        Debug.LogError("[GeminiRemoteTTSHandler] Response JSON did not contain 'audioContent'.");
                        return null;
                    }

                    byte[] rawWavBytes = Convert.FromBase64String(responseData.audioContent);
                    return WavToAudioClipParser(rawWavBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GeminiRemoteTTSHandler] JSON/Wav Processing Exception: {ex.Message}");
                    return null;
                }
            }
        }

        private AudioClip WavToAudioClipParser(byte[] wavBytes)
        {
            int headerOffset = 44;
            int sampleCount = (wavBytes.Length - headerOffset) / 2;
            float[] audioData = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short bitSample = BitConverter.ToInt16(wavBytes, headerOffset + (i * 2));
                audioData[i] = bitSample / 32768.0f; 
            }

            int sampleRate = BitConverter.ToInt32(wavBytes, 24);

            AudioClip audioClip = AudioClip.Create("GCP_NativeStudioVoice", sampleCount, 1, sampleRate, false);
            audioClip.SetData(audioData, 0);
            return audioClip;
        }

        #region Serialized Structures
        [System.Serializable]
        public class SynthesisInput
        {
            public string text;
        }

        [System.Serializable]
        public class VoiceSelectionParams
        {
            public string languageCode;
            public string name;
            public string ssmlGender = "NEUTRAL";
        }

        [System.Serializable]
        public class AudioConfig
        {
            public string audioEncoding; 
            public float speakingRate;
            public float pitch = 0f;
            public float volumeGainDb = 0f;
        }

        [System.Serializable]
        public class SynthesizeSpeechRequest
        {
            public SynthesisInput input;
            public VoiceSelectionParams voice;
            public AudioConfig audioConfig;
            public string model; // Added model field
        }

        [System.Serializable]
        public class SynthesizeSpeechResponse
        {
            public string audioContent;
        }
        #endregion
    }
}