using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Manages the connection to the Gemini API and stores the current mystery state.
/// This version is "Strongly Typed" to avoid the CS0656 'dynamic' error in Unity.
/// </summary>
public class GeminiConnectionManager : MonoBehaviour
{
    // The environment provides the API key at runtime.
    // IF RUNNING IN UNITY EDITOR: You must paste your API key here or the request will fail.
    [SerializeField] private string apiKey = "AIzaSyDZFQ5SqXgFCpA7fvDcGOWVLXOjKrZYc2A";
    private string model = "gemini-2.5-flash-preview-09-2025";

    [Header("Current Game State")]
    public ScenarioData currentScenario;

    // The Callback system for the response
    public delegate void ScenarioCallback(ScenarioData data, string error);

    /// <summary>
    /// Starts the generation process. Pass a method here to handle the result.
    /// </summary>
    public void GenerateScenario(ScenarioCallback callback)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("Gemini API Key is missing! The request will likely fail or get stuck in retries.");
        }
        StartCoroutine(PostScenarioRequest(callback));
    }

    private IEnumerator PostScenarioRequest(ScenarioCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        string systemPrompt = @"You are a master mystery writer. Generate a murder mystery scenario in JSON.
        
        RULES:
        1. Exactly 5 suspects.
        2. Group A (The Liars): Exactly 2 suspects have is_in_group_a = true (The Killer + 1 Red Herring).
        3. Group B (The Motivated): 2 to 3 suspects have is_in_group_b = true (Must include the Killer).
        4. Group C (The Capable): 2 to 3 suspects have is_in_group_c = true (Must include the Killer).
        5. THE KILLER: Only one suspect can have all three flags (A, B, and C) set to true.
        6. Rumors: Each suspect must have a rumor about every other suspect's potential motive.
        7. Minor Secrets: Red Herrings may have a secret, others can be null.

        JSON_STRUCTURE_EXAMPLE:
        {
          ""victim_name"": ""Name"",
          ""victim_occupation"": ""Job"",
          ""victim_discovery_details"": ""Details"",
          ""murder_weapon"": ""Weapon"",
          ""murder_location"": ""Location"",
          ""suspects"": [
            {
              ""name"": ""Suspect Name"",
              ""personality"": ""Traits"",
              ""alibi_statement"": ""Story"",
              ""minor_secret"": ""Secret or null"",
              ""rumors"": { ""Other Suspect Name"": ""Rumor text"" },
              ""is_in_group_a"": true,
              ""is_in_group_b"": false,
              ""is_in_group_c"": true,
              ""is_killer"": false
            }
          ]
        }";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = "Generate a new mystery scenario based on the rules." } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);

        // Exponential backoff retry logic (up to 5 tries)
        int retries = 0;
        bool success = false;
        string lastError = "";

        while (retries < 5 && !success)
        {
            Debug.Log($"Gemini API Attempt {retries + 1}/5...");

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);

                        if (response != null && response.candidates != null && response.candidates.Count > 0)
                        {
                            string jsonText = response.candidates[0].content.parts[0].text;
                            ScenarioData scenario = JsonConvert.DeserializeObject<ScenarioData>(jsonText);

                            currentScenario = scenario;
                            success = true;
                            Debug.Log("<color=green>Gemini Scenario Generated Successfully!</color>");
                            callback?.Invoke(scenario, null);
                        }
                        else
                        {
                            lastError = "API returned an empty or malformed response. Check safety settings in Google AI Studio.";
                            Debug.LogWarning(lastError);
                            retries++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = "Parsing Error: " + ex.Message;
                        Debug.LogError(lastError);
                        retries++;
                    }
                }
                else
                {
                    // Log the actual server response text to see the error details (like invalid API key)
                    lastError = $"API Error ({request.responseCode}): {request.error}\nDetails: {request.downloadHandler.text}";
                    Debug.LogWarning(lastError);
                    retries++;
                }

                if (!success && retries < 5)
                {
                    float waitTime = Mathf.Pow(2, retries);
                    Debug.Log($"Retrying in {waitTime} seconds...");
                    yield return new WaitForSeconds(waitTime);
                }
            }
        }

        if (!success)
        {
            Debug.LogError($"<color=red>Gemini Request Failed permanently.</color> Final error: {lastError}");
            callback?.Invoke(null, "Failed after max retries. Check console for details.");
        }
    }

    // --- API Response Wrappers (These classes allow parsing without 'dynamic') ---
    [Serializable] public class GeminiResponseWrapper { public List<GeminiCandidate> candidates; }
    [Serializable] public class GeminiCandidate { public GeminiContent content; }
    [Serializable] public class GeminiContent { public List<GeminiPart> parts; }
    [Serializable] public class GeminiPart { public string text; }
}

