using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Manages Text-to-Speech using the Gemini 2.5 Flash TTS model.
/// Decodes raw PCM16 (audio/L16) data directly into Unity AudioClips.
/// Fixed: Robust sample rate parsing to prevent "Frequency must be greater than 0" error.
/// </summary>
public class GeminiTTSHandler : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = ""; 
    private string modelId = "gemini-2.5-flash-preview-tts";

    [Header("Audio Output")]
    public AudioSource voiceSource;

    public delegate void TTSCallback(bool success, string error);

    /// <summary>
    /// Generates and plays a voice for the given text.
    /// Available voices: Zephyr, Puck, Charon, Kore, Fenrir, Leda, Orus, etc.
    /// </summary>
    public void PlayVoice(string text, string voiceName, TTSCallback callback = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("GeminiTTSHandler: API Key is missing!");
            callback?.Invoke(false, "API Key Missing");
            return;
        }

        if (string.IsNullOrEmpty(voiceName))
        {
            voiceName = "Puck";
        }

        StartCoroutine(PostTTSRequest(text, voiceName, callback));
    }

    private IEnumerator PostTTSRequest(string text, string voiceName, TTSCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={apiKey.Trim()}";

        // Multimodal TTS Payload
        var payload = new
        {
            contents = new[] { 
                new { parts = new[] { new { text = text } } } 
            },
            generationConfig = new {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new {
                    voiceConfig = new {
                        prebuiltVoiceConfig = new {
                            voiceName = voiceName
                        }
                    }
                }
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try 
                {
                    var res = JsonConvert.DeserializeObject<GeminiTTSResponse>(request.downloadHandler.text);
                    
                    if (res?.candidates != null && res.candidates.Length > 0)
                    {
                        var part = res.candidates[0].content.parts[0];
                        if (part.inlineData != null && !string.IsNullOrEmpty(part.inlineData.data))
                        {
                            // 1. Convert Base64 string to raw bytes
                            byte[] pcmBytes = Convert.FromBase64String(part.inlineData.data);
                            
                            // 2. Robust sample rate parsing (e.g., "audio/L16;rate=24000")
                            int sampleRate = 24000; // Standard Gemini Default
                            string mime = part.inlineData.mimeType;

                            if (!string.IsNullOrEmpty(mime) && mime.Contains("rate="))
                            {
                                try 
                                {
                                    string[] mimeParts = mime.Split('=');
                                    if (mimeParts.Length > 1)
                                    {
                                        // Attempt to parse the rate, ensuring it's greater than 0
                                        if (int.TryParse(mimeParts[1], out int parsedRate) && parsedRate > 0)
                                        {
                                            sampleRate = parsedRate;
                                        }
                                    }
                                }
                                catch { /* Fall back to 24000 if split fails */ }
                            }

                            // 3. Convert PCM16 bytes to Unity AudioClip
                            AudioClip clip = Pcm16ToAudioClip(pcmBytes, "GeminiVoice", sampleRate);

                            if (clip != null && voiceSource != null)
                            {
                                voiceSource.clip = clip;
                                voiceSource.Play();
                                callback?.Invoke(true, null);
                            }
                        }
                        else
                        {
                            callback?.Invoke(false, "No audio data found in response parts.");
                        }
                    }
                    else
                    {
                        callback?.Invoke(false, "No candidates returned by API.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"GeminiTTSHandler: Error processing audio: {ex.Message}");
                    callback?.Invoke(false, ex.Message);
                }
            }
            else
            {
                Debug.LogError($"GeminiTTSHandler: API Error: {request.downloadHandler.text}");
                callback?.Invoke(false, request.error);
            }
        }
    }

    /// <summary>
    /// Converts raw 16-bit PCM bytes into a Unity AudioClip.
    /// </summary>
    private AudioClip Pcm16ToAudioClip(byte[] pcmBytes, string clipName, int sampleRate)
    {
        // Safety check to prevent Unity crash if rate is somehow still 0
        if (sampleRate <= 0) sampleRate = 24000;

        int sampleCount = pcmBytes.Length / 2; // 2 bytes per 16-bit sample
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // BitConverter handles the two bytes into a short
            short sample = BitConverter.ToInt16(pcmBytes, i * 2);
            // Normalize short (-32768 to 32767) to float (-1.0 to 1.0)
            samples[i] = sample / 32768f;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Response Classes
    [Serializable]
    public class GeminiTTSResponse
    {
        public Candidate[] candidates;
    }

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public InlineData inlineData;
    }

    [Serializable]
    public class InlineData
    {
        public string mimeType;
        public string data; // Base64 PCM16
    }
}