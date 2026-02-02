using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;

/// <summary>
/// Manages the connection to the Gemini API and stores the current mystery state.
/// Updated to include specific Access descriptions, Victim Biography, and Relationships.
/// </summary>
public class GeminiConnectionManager : MonoBehaviour
{
    [SerializeField] private string apiKey = "";
    private string model = "gemini-3-flash-preview";
    public ScenarioData currentScenario;

    [Header("Debug Settings")]
    [SerializeField] private TMP_Text debugDisplayField;

    [Header("References")]
    public NotebookManager notebookManager;

    public delegate void ScenarioCallback(ScenarioData data, string error);
    public delegate void InterrogationCallback(string response, string error);
    public delegate void JudgeCallback(bool isCorrect, string feedback, string error);

    // --- CALL 1: SCENARIO GENERATION ---
    public void GenerateScenario(ScenarioCallback callback)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API Key is missing!");
            return;
        }
        StartCoroutine(PostScenarioRequest(callback));
    }

    private IEnumerator PostScenarioRequest(ScenarioCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        // UPDATED SYSTEM PROMPT: Now requires 'access_to_weapon_description', 'relationship', and 'victim_biography'
        string systemPrompt = @"You are a master mystery writer. Generate a murder mystery scenario in JSON.
        
        LOGIC RULES (The Triple-Filter):
        1. Exactly 5 suspects.
        2. The Liars (No Alibi): Exactly 2 suspects have has_no_alibi = true (Killer + Red Herring).
        3. The Motivated: Exactly 2 to 3 suspects have has_motive = true (Must include the Killer).
        4. The Capable (Access): Exactly 2 to 3 suspects have has_access_to_weapon = true (Must include the Killer).
           - CRITICAL: For these suspects, you MUST provide a 'access_to_weapon_description' explaining HOW they reached/obtained the weapon.
        5. THE KILLER: The ONLY suspect with all three flags true.
        
        STORY RULES:
        6. Victim Biography: Provide 2-3 sentences of background info on the victim's life and social standing.
        7. Relationship: Define a clear connection to the victim (e.g., 'Business Partner', 'Scorned Lover').
        8. Personality: Adjectives only. No spoilers.
        9. Access Description: Be specific (e.g., 'Possesses the master key', 'Was left alone in the kitchen with the knives').
        10. RANDOMIZATION: You MUST randomize the index of the killer in the suspects list. Do NOT always place the killer at the first position (index 0). The killer should appear randomly at any index (0-4).
        11. SOLVABILITY: Every 'Motive', 'Access', and 'False Alibi' MUST have a corresponding clue hidden in at least one other suspect's 'rumors' dictionary. The player must be able to solve the case purely by cross-referencing these rumors.

        JSON_STRUCTURE_EXAMPLE:
        {
          ""victim_name"": ""Name"",
          ""victim_occupation"": ""Job"",
          ""victim_biography"": ""Context about their life and history."",
          ""victim_discovery_details"": ""Details"",
          ""murder_weapon"": ""Weapon"",
          ""murder_location"": ""Location"",
          ""suspects"": [
            {
              ""name"": ""Name"",
              ""relationship"": ""Connection to victim"",
              ""personality"": ""Trait"",
              ""motive"": ""Reason or null"",
              ""access_to_weapon_description"": ""How they got the weapon or null"",
              ""alibi_statement"": ""Alibi"",
              ""minor_secret"": ""Secret or null"",
              ""rumors"": { ""OtherSuspectName"": ""I saw them sneaking into the vault earlier."" },
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
        if (debugDisplayField != null) debugDisplayField.text = "Generating Scenario...";

        using (UnityWebRequest request = CreateRequest(url, jsonPayload))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                    currentScenario = JsonConvert.DeserializeObject<ScenarioData>(response.candidates[0].content.parts[0].text);
                    if (debugDisplayField != null) debugDisplayField.text = currentScenario.ToString();

                    // If a NotebookManager has been assigned in the inspector, ensure it has
                    // a reference to this connection manager and populate the victim page.
                    if (notebookManager != null)
                    {
                        if (notebookManager.connectionManager == null) notebookManager.connectionManager = this;
                        notebookManager.PopulateVictimPage();
                    }

                    callback?.Invoke(currentScenario, null);
                }
                catch (Exception ex) { callback?.Invoke(null, ex.Message); }
            }
            else { callback?.Invoke(null, request.error); }
        }
    }

    // --- CALL 2: INTERROGATION ---
    public void SpeakWithSuspect(string question, SuspectData suspect, InterrogationCallback callback)
    {
        StartCoroutine(PostInterrogationRequest(question, suspect, callback));
    }

    private IEnumerator PostInterrogationRequest(string question, SuspectData suspect, InterrogationCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        string systemPrompt = $@"You are roleplaying as {suspect.name}.
        RELATIONSHIP TO VICTIM: {suspect.relationship}.
        TRAITS: {suspect.personality}.
        ALIBI: {suspect.alibi_statement}.
        MOTIVE: {(suspect.has_motive ? suspect.motive : "None.")}.
        ACCESS: {(suspect.has_access_to_weapon ? suspect.access_to_weapon_description : "You had no access.")}.
        GUILT: {(suspect.is_killer ? "YOU ARE THE KILLER." : "INNOCENT.")}.
        RUMORS: {JsonConvert.SerializeObject(suspect.rumors)}.
        RULES: Stay in character. Speak appropriately regarding your relationship to the deceased. 1-2 sentences.";

        suspect.chatHistory.Add(new GeminiContent { role = "user", parts = new List<GeminiPart> { new GeminiPart { text = question } } });

        var payload = new
        {
            contents = suspect.chatHistory.ToArray(),
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } }
        };

        using (UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload)))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                string reply = response.candidates[0].content.parts[0].text;
                suspect.chatHistory.Add(new GeminiContent { role = "model", parts = new List<GeminiPart> { new GeminiPart { text = reply } } });
                callback?.Invoke(reply, null);
            }
            else { callback?.Invoke(null, request.error); }
        }
    }

    // --- CALL 3: THE JUDGE ---
    public void JudgeAccusation(string accusedName, string motiveReasoning, string accessReasoning, JudgeCallback callback)
    {
        if (currentScenario == null) return;
        StartCoroutine(PostJudgeRequest(accusedName, motiveReasoning, accessReasoning, callback));
    }

    private IEnumerator PostJudgeRequest(string accusedName, string motive, string access, JudgeCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        string systemPrompt = $@"You are the Judge. Compare the player's accusation to this TRUTH:
        {JsonConvert.SerializeObject(currentScenario)}

        EVALUATION:
        1. WHO: Does name match is_killer=true?
        2. MOTIVE: Does reasoning match 'motive'?
        3. ACCESS: Does reasoning match 'access_to_weapon_description'?

        OUTPUT JSON: is_correct (bool), feedback (string).";

        string userQuery = $"Accused: {accusedName}\nMotive: {motive}\nAccess: {access}";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = userQuery } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        is_correct = new { type = "BOOLEAN" },
                        feedback = new { type = "STRING" }
                    },
                    required = new[] { "is_correct", "feedback" }
                }
            }
        };

        using (UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload)))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                GeminiResponseWrapper response = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                JudgeResult result = JsonConvert.DeserializeObject<JudgeResult>(response.candidates[0].content.parts[0].text);
                callback?.Invoke(result.is_correct, result.feedback, null);
            }
            else { callback?.Invoke(false, null, request.error); }
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

    [Serializable] public class GeminiResponseWrapper { public List<GeminiCandidate> candidates; }
    [Serializable] public class GeminiCandidate { public GeminiContent content; }
    [Serializable] public class GeminiContent { public string role; public List<GeminiPart> parts; }
    [Serializable] public class GeminiPart { public string text; }
    [Serializable] private class JudgeResult { public bool is_correct; public string feedback; }
}