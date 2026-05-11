using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Manages communication with the ElevenLabs API to generate suspect voices.
/// Uses the Turbo v2.5 model for ultra-low latency.
/// Fixed: NotSupportedException when accessing .text on Audio DownloadHandlers.
/// </summary>
public class ElevenLabsTTSHandler : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string modelId = "eleven_turbo_v2_5";

    [Header("Audio Output")]
    public AudioSource voiceSource;

    // Delegate to notify when audio is ready or failed
    public delegate void TTSCallback(bool success, string error);

    /// <summary>
    /// Sends text to ElevenLabs and plays the resulting audio.
    /// </summary>
    public void PlayVoice(string text, string voiceId, TTSCallback callback = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("ElevenLabs API Key is missing!");
            callback?.Invoke(false, "API Key Missing");
            return;
        }

        if (string.IsNullOrEmpty(voiceId))
        {
            Debug.LogWarning("No Voice ID provided for suspect.");
            callback?.Invoke(false, "Voice ID Missing");
            return;
        }

        StartCoroutine(PostTTSRequest(text, voiceId, callback));
    }

    private IEnumerator PostTTSRequest(string text, string voiceId, TTSCallback callback)
    {
        string url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

        var payload = new
        {
            text = text,
            model_id = modelId,
            voice_settings = new
            {
                stability = 0.5f,
                similarity_boost = 0.75f,
                style = 0.0f,
                use_speaker_boost = true
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // We use DownloadHandlerAudioClip for the MPEG/MP3 response
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", apiKey.Trim());

            float ttsStart = Time.realtimeSinceStartup;
            Debug.Log($"Generation Debug: TTS request sent to ElevenLabs...");
            yield return request.SendWebRequest();
            float ttsDuration = Time.realtimeSinceStartup - ttsStart;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Generation Debug: TTS generated (ready to play): {ttsDuration:F1}s");
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip != null && voiceSource != null)
                {
                    voiceSource.clip = clip;
                    voiceSource.Play();
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, "Failed to generate AudioClip");
                }
            }
            else
            {
                // FIX: DownloadHandlerAudioClip does not support .text property.
                // We must manually convert the raw byte data to a string to read the error message.
                string errorDetail = "Unknown Error";
                if (request.downloadHandler.data != null && request.downloadHandler.data.Length > 0)
                {
                    errorDetail = Encoding.UTF8.GetString(request.downloadHandler.data);
                }
                else
                {
                    errorDetail = request.error;
                }

                Debug.LogError($"ElevenLabs API Error: {errorDetail}");
                callback?.Invoke(false, errorDetail);
            }
        }
    }
}