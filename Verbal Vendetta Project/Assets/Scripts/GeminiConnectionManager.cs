using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class GeminiConnectionManager : MonoBehaviour
{
    [SerializeField] private string apiKey = "";
    private string model = "gemini-2.5-flash-preview-09-2025";
    public ScenarioData currentScenario;

    public delegate void ScenarioCallback(ScenarioData data, string error);
    public delegate void InterrogationCallback(string response, string error);

    public void GenerateScenario(ScenarioCallback callback)
    {
        StartCoroutine(PostScenarioRequest(callback));
    }

    private IEnumerator PostScenarioRequest(ScenarioCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        string systemPrompt = @"You are a master mystery writer. Generate a murder mystery scenario in JSON.
        
        LOGIC RULES (The Triple-Filter):
        1. Exactly 5 suspects.
        2. The Liars: Exactly 2 suspects have has_no_alibi = true.
        3. The Motivated: Exactly 2 to 3 suspects have has_motive = true. ONLY these have a 'motive' string.
        4. The Capable: 2 to 3 suspects have has_access_to_weapon = true.
        5. THE KILLER: The ONLY suspect with all three flags set to true.
        
        STORY RULES (STRICT SEPARATION):
        6. Personality: Use ONLY adjectives and social traits. 
        7. Motive: This is where you put the 'Why'. It must be a specific secret event or pressure.
        8. Rumors: Suspects ONLY gossip about the 'motive' of others where has_motive is true.

        JSON_STRUCTURE_EXAMPLE:
        {
          ""victim_name"": ""Name"",
          ""victim_occupation"": ""Job"",
          ""victim_discovery_details"": ""Details"",
          ""murder_weapon"": ""Weapon"",
          ""murder_location"": ""Location"",
          ""suspects"": [
            {
              ""name"": ""Name"",
              ""personality"": ""Short-tempered"",
              ""motive"": ""Owed money"",
              ""alibi_statement"": ""Alibi"",
              ""minor_secret"": ""Secret"",
              ""rumors"": { ""SuspectName"": ""Rumor text"" },
              ""has_no_alibi"": true,
              ""has_motive"": true,
              ""has_access_to_weapon"": true,
              ""is_killer"": true
            }
          ]
        }";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = "Generate a new mystery following the logic rules." } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = CreateRequest(url, jsonPayload))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                    string jsonText = response.candidates[0].content.parts[0].text;
                    currentScenario = JsonConvert.DeserializeObject<ScenarioData>(jsonText);
                    callback?.Invoke(currentScenario, null);
                }
                catch (Exception ex)
                {
                    callback?.Invoke(null, "Parsing Error: " + ex.Message);
                }
            }
            else
            {
                callback?.Invoke(null, "API Error: " + request.error);
            }
        }
    }

    // --- INTERROGATION LOGIC WITH MEMORY ---

    public void SpeakWithSuspect(string question, SuspectData suspect, InterrogationCallback callback)
    {
        StartCoroutine(PostInterrogationRequest(question, suspect, callback));
    }

    private IEnumerator PostInterrogationRequest(string question, SuspectData suspect, InterrogationCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        // 1. Setup the Persona Instructions
        string systemPrompt = $@"You are roleplaying as a suspect in a murder mystery.
        NAME: {suspect.name}.
        TRAITS: {suspect.personality}.
        ALIBI: {suspect.alibi_statement}.
        MOTIVE: {(suspect.has_motive ? suspect.motive : "None.")}.
        GUILT: {(suspect.is_killer ? "YOU ARE THE KILLER. Lie and deflect." : "INNOCENT. Tell the truth about your alibi.")}.
        RUMORS YOU KNOW: {JsonConvert.SerializeObject(suspect.rumors)}.

        RULES:
        - Stay in character. Brief responses (1-2 sentences).
        - Use the conversation history to stay consistent. If the player repeats a question or catches a contradiction, react accordingly.";

        // 2. Manage the Memory (Chat History)
        // Add the new user question to this suspect's personal history
        suspect.chatHistory.Add(new GeminiContent
        {
            role = "user",
            parts = new List<GeminiPart> { new GeminiPart { text = question } }
        });

        // 3. Build the Payload with the FULL history
        var payload = new
        {
            contents = suspect.chatHistory.ToArray(), // Send the whole conversation history!
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = CreateRequest(url, jsonPayload))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                    string reply = response.candidates[0].content.parts[0].text;

                    // 4. Save the AI's reply to the history so it remembers for next time
                    suspect.chatHistory.Add(new GeminiContent
                    {
                        role = "model",
                        parts = new List<GeminiPart> { new GeminiPart { text = reply } }
                    });

                    callback?.Invoke(reply, null);
                }
                catch (Exception ex)
                {
                    callback?.Invoke(null, "Parsing Error: " + ex.Message);
                }
            }
            else
            {
                callback?.Invoke(null, "API Error: " + request.error);
            }
        }
    }

    private UnityWebRequest CreateRequest(string url, string json)
    {
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    // Helper classes updated for History support
    [Serializable] public class GeminiResponseWrapper { public List<GeminiCandidate> candidates; }
    [Serializable] public class GeminiCandidate { public GeminiContent content; }

    [Serializable]
    public class GeminiContent
    {
        public string role; // "user" or "model"
        public List<GeminiPart> parts;
    }

    [Serializable] public class GeminiPart { public string text; }
}