using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Uses Gemini Multimodal capabilities to transcribe audio data.
/// Reverted to gemini-2.5-flash-preview-09-2025 as it is the only model found by the current endpoint.
/// </summary>
public class GeminiSTTHandler : MonoBehaviour
{
    [SerializeField] private string apiKey = "";

    // Using the specific preview model that was successfully found previously.
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

        // Safety check: Don't send empty or tiny audio files
        if (wavData == null || wavData.Length < 100)
        {
            callback?.Invoke("", null);
            return;
        }

        StartCoroutine(PostSTTRequest(wavData, callback));
    }

    private UnityWebRequest currentRequest;

    /// <summary>
    /// Cancels any ongoing STT request.
    /// </summary>
    public void CancelTranscription()
    {
        if (currentRequest != null)
        {
            currentRequest.Abort();
            currentRequest.Dispose(); // Ensure it's cleaned up
            currentRequest = null;
        }
        StopAllCoroutines(); // Also stop the polling/waiting
    }

    private IEnumerator PostSTTRequest(byte[] wavData, STTCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        // Constructing the multimodal payload using camelCase for API compatibility
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

        int maxAttempts = 3;
        float baseDelay = 1.0f; // seconds
        bool finished = false;
        string lastError = null;

        for (int attempt = 1; attempt <= maxAttempts && !finished; attempt++)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                currentRequest = request; // Track current request

                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30; // seconds

                yield return request.SendWebRequest();

                // Clear current request ref as it's done (or about to be disposed by 'using')
                if (currentRequest == request) currentRequest = null;

                // Log some additional diagnostics for intermittent failures
                long responseCode = request.responseCode;
                string responseText = string.Empty;
                try { responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty; } catch { responseText = "<unable to read response>"; }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(responseText);

                        if (response?.candidates != null && response.candidates.Count > 0)
                        {
                            var candidate = response.candidates[0];
                            // Defensive checks: some API responses may include candidate.content.role without parts
                            if (candidate?.content != null && candidate.content.parts != null && candidate.content.parts.Count > 0 && !string.IsNullOrEmpty(candidate.content.parts[0].text))
                            {
                                string result = candidate.content.parts[0].text;
                                callback?.Invoke(result.Trim(), null);
                            }
                            else
                            {
                                // No transcript parts present -> treat as empty transcription (silence or non-text response)
                                callback?.Invoke("", null);
                            }
                        }
                        else
                        {
                            callback?.Invoke("", null);
                        }

                        finished = true;
                    }
                    catch (JsonException jex)
                    {
                        lastError = "STT Parsing Error: " + jex.Message;
                        Debug.LogError($"STT Parsing Error (attempt {attempt}): {jex.Message}\nResponseCode: {responseCode}\nResponseText: {responseText}");
                        // don't finish, maybe transient malformed response
                    }
                    catch (Exception ex)
                    {
                        lastError = "STT Unknown Error: " + ex.Message;
                        Debug.LogError($"STT Unknown Error (attempt {attempt}): {ex.Message}\nResponseCode: {responseCode}\nResponseText: {responseText}");
                    }
                }
                else
                {
                    // Detailed logging to help diagnose intermittent issues
                    // Check if aborted manually
                    if (request.error == "Request aborted") 
                    {
                         // Silent fail / Log as info
                         lastError = "Request Cancelled";
                         finished = true; // Stop retrying
                    }
                    else
                    {
                        lastError = $"STT API Error: {request.error} (code: {responseCode})";
                        Debug.LogError($"STT API Error (attempt {attempt}): {request.error} | ResponseCode: {responseCode} | ResponseText: {responseText}");
                    }
                }
            }

            if (!finished && attempt < maxAttempts)
            {
                // exponential backoff with simple jitter
                float delay = baseDelay * Mathf.Pow(2, attempt - 1) + UnityEngine.Random.Range(0f, 0.5f);
                yield return new WaitForSeconds(delay);
            }
        }

        if (!finished && lastError != "Request Cancelled")
        {
            callback?.Invoke(null, lastError ?? "STT API Error: Unknown");
        }
    }

    [Serializable] public class GeminiResponseWrapper { public List<Candidate> candidates; }
    [Serializable] public class Candidate { public Content content; }
    [Serializable] public class Content { public List<Part> parts; }
    [Serializable] public class Part { public string text; }
}