using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Uses Gemini 3.0 Multimodal capabilities to transcribe audio data.
/// This script handles the conversion of audio bytes into a text transcript.
/// </summary>
public class GeminiSTTHandler : MonoBehaviour
{
    [SerializeField] private string apiKey = "";

    // Using Gemini 3.0 Flash for low-latency multimodal processing
    private string model = "gemini-2.5-flash-preview-09-2025";

    public delegate void STTCallback(string transcription, string error);

    /// <summary>
    /// Sends WAV byte data to Gemini for transcription.
    /// </summary>
    /// <param name="wavData">The raw bytes of the WAV file.</param>
    /// <param name="callback">Callback returning the transcript string.</param>
    public void TranscribeAudio(byte[] wavData, STTCallback callback)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            callback?.Invoke(null, "API Key Missing for STT");
            return;
        }
        StartCoroutine(PostSTTRequest(wavData, callback));
    }

    private IEnumerator PostSTTRequest(byte[] wavData, STTCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        // Constructing the multimodal payload
        // NOTE: inlineData and mimeType must use camelCase for the Google API
        var payload = new
        {
            contents = new[] {
                new {
                    parts = new object[] {
                        new { text = "Transcribe the following audio precisely. Respond ONLY with the transcript text. If no speech is detected, return an empty string." },
                        new { inlineData = new { mimeType = "audio/wav", data = Convert.ToBase64String(wavData) } }
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
                    var response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);

                    if (response?.candidates != null && response.candidates.Count > 0)
                    {
                        string result = response.candidates[0].content.parts[0].text;
                        callback?.Invoke(result.Trim(), null);
                    }
                    else
                    {
                        callback?.Invoke("", null); // Return empty if no candidates found
                    }
                }
                catch (Exception ex)
                {
                    callback?.Invoke(null, "STT Parsing Error: " + ex.Message);
                }
            }
            else
            {
                // Log detail to console for debugging
                string errorDetail = request.downloadHandler.text;
                Debug.LogError($"STT API Error Detail: {errorDetail}");
                callback?.Invoke(null, "STT API Error: " + request.error);
            }
        }
    }

    // JSON response wrappers
    [Serializable] public class GeminiResponseWrapper { public List<Candidate> candidates; }
    [Serializable] public class Candidate { public Content content; }
    [Serializable] public class Content { public List<Part> parts; }
    [Serializable] public class Part { public string text; }
}