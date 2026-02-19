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
/// Updated with refined Triple-Filter logic, Timeline anchoring, Rumor cross-referencing,
/// and Gender-based Voice ID assignment.
/// </summary>
public class GeminiConnectionManager : MonoBehaviour
{
    [SerializeField] private string apiKey = "";

    private string model = "gemini-3-flash-preview"; // Updated to model that supports audio generation or stick to 3-flash-preview? Stuck to user's choice but 1.5-flash is safer for audio. I'll keep the private field as is but maybe update the default value if needed.
    // User had "gemini-3-flash-preview". I should probably leave 'model' alone unless necessary, but the lists need update.

    public ScenarioData currentScenario;

    public bool testing = false;

    private const string TEST_SCENARIO_JSON = @"
{
  ""victim_name"": ""Silas Vance"",
  ""victim_occupation"": ""Billionaire Tech Recluse"",
  ""victim_biography"": ""Silas Vance was a ruthless innovator who spent his final years isolated in his Gothic mansion. He was notoriously paranoid and was known for making more legal enemies than friends during his career."",
  ""victim_discovery_details"": ""Found slumped over his mahogany desk with a single puncture wound in his palm and a spilled bottle of specialty blue ink. The emergency alert on his desk had been disabled."",
  ""murder_time"": ""11:45 PM"",
  ""murder_date"": ""October 30th"",
  ""interrogation_date"": ""October 31st"",
  ""murder_weapon"": ""Poisoned Vintage Fountain Pen"",
  ""murder_location"": ""The Mansion Library"",
  ""suspects"": [
    {
      ""name"": ""Julian Thorne"",
      ""gender"": ""Male"",
      ""relationship"": ""Business Partner"",
      ""personality"": ""Nervous, ambitious, debt-ridden"",
      ""voice_id"": ""Achird"",
      ""model_id"": 0,
      ""motive"": ""Julian owed Silas millions in failed tech investments and was about to be sued into bankruptcy."",
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was in the smoking room on the west wing, trying to relax and clearing my head after a long day."",
      ""minor_secret"": ""He has been skimming small amounts from the company payroll for years."",
      ""rumors"": {
        ""Elena Vance"": ""I saw Elena taking a key from the butler's station that opens Silas's private stationery cabinet.""
      },
      ""has_no_alibi"": true,
      ""has_motive"": true,
      ""has_access_to_weapon"": false,
      ""is_killer"": false
    },
    {
      ""name"": ""Marcus Reed"",
      ""gender"": ""Male"",
      ""relationship"": ""Ex-Security Head"",
      ""personality"": ""Resentful, disciplined, observant"",
      ""voice_id"": ""Algenib"",
      ""model_id"": 0,
      ""motive"": ""Silas fired him without a pension last month after a minor security lapse."",
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was at 'The Rusty Anchor' pub downtown until closing time. The bartender can vouch for me."",
      ""minor_secret"": ""He still has a copy of the mansion's architectural blueprints."",
      ""rumors"": {
        ""Clara Hughes"": ""Clara is the only one Silas let handle his pen collection; she was cleaning them with a strange solvent yesterday.""
      },
      ""has_no_alibi"": false,
      ""has_motive"": true,
      ""has_access_to_weapon"": false,
      ""is_killer"": false
    },
    {
      ""name"": ""Elena Vance"",
      ""gender"": ""Female"",
      ""relationship"": ""Wife"",
      ""personality"": ""Cold, calculated, elegant"",
      ""voice_id"": ""Achernar"",
      ""model_id"": 1,
      ""motive"": ""Silas was planning to divorce her and update his will to exclude her entirely by morning."",
      ""access_to_weapon_description"": ""She stole the key to the glass display case and used her knowledge of the library's security bypass to coat the pen nib with a fast-acting toxin."",
      ""alibi_statement"": ""I had retired to my bedroom early with a migraine and didn't leave until the staff found his body."",
      ""minor_secret"": ""She has already been in contact with a high-profile divorce attorney."",
      ""rumors"": {
        ""Marcus Reed"": ""I heard Silas shouting at Marcus that he was a 'useless failure' just hours before the firing.""
      },
      ""has_no_alibi"": true,
      ""has_motive"": true,
      ""has_access_to_weapon"": true,
      ""is_killer"": true
    },
    {
      ""name"": ""Clara Hughes"",
      ""gender"": ""Female"",
      ""relationship"": ""Personal Secretary"",
      ""personality"": ""Efficient, loyal, overworked"",
      ""voice_id"": ""Aoede"",
      ""model_id"": 1,
      ""motive"": null,
      ""access_to_weapon_description"": ""Clara is responsible for the daily maintenance, ink-filling, and cleaning of Silas's extensive fountain pen collection."",
      ""alibi_statement"": ""I was in the kitchen preparing the midnight tea tray, which the cook can confirm."",
      ""minor_secret"": ""She is secretly writing a tell-all memoir about the Vance family."",
      ""rumors"": {
        ""Elena Vance"": ""I heard Elena and Silas arguing about a 'new will' and 'signing papers' late last night.""
      },
      ""has_no_alibi"": false,
      ""has_motive"": false,
      ""has_access_to_weapon"": true,
      ""is_killer"": false
    },
    {
      ""name"": ""Father Dominic"",
      ""gender"": ""Male"",
      ""relationship"": ""Family Priest"",
      ""personality"": ""Stoic, soft-spoken, judgmental"",
      ""voice_id"": ""Algieba"",
      ""model_id"": 0,
      ""motive"": null,
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was in the mansion's chapel performing my nightly prayers. I find the silence there very centering."",
      ""minor_secret"": ""He was once a professional locksmith before entering the priesthood."",
      ""rumors"": {
        ""Julian Thorne"": ""Julian claims he was in the smoking room, but I walked past it at 11:45 PM and the room was completely empty.""
      },
      ""has_no_alibi"": false,
      ""has_motive"": false,
      ""has_access_to_weapon"": false,
      ""is_killer"": false
    }
  ]
}";

    [Header("Dependencies")]
    public SuspectManager suspectManager;
    
    // Removed local lists for Voices and Model Indices

    [Header("Debug Settings")]
    [SerializeField] private TMP_Text debugDisplayField;

    [Header("UI References")]
    public NotebookManager notebookManager;

    // --- DELEGATES ---
    public delegate void ScenarioCallback(ScenarioData data, string error);
    public delegate void InterrogationCallback(SuspectResponse response, string error);
    public delegate void ReactionCallback(FaceAnimator.EmotionType emotion, float stressChange, string error);
    public delegate void JudgeCallback(string headline, string article, bool isCorrect, string error);

    // --- CALL 1: SCENARIO GENERATION ---
    public void GenerateScenario(ScenarioCallback callback)
    {
        if (testing)
        {
            try
            {
                currentScenario = JsonConvert.DeserializeObject<ScenarioData>(TEST_SCENARIO_JSON);
                if (debugDisplayField != null) debugDisplayField.text = currentScenario.ToString();
                if (notebookManager != null) notebookManager.PopulateVictimPage();
                callback?.Invoke(currentScenario, null);
            }
            catch (Exception ex)
            {
                callback?.Invoke(null, "Testing JSON Error: " + ex.Message);
            }
            return;
        }
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API Key is missing!");
            callback?.Invoke(null, "API Key Missing");
            return;
        }
        
        if (suspectManager == null)
        {
             Debug.LogError("SuspectManager reference missing!");
             callback?.Invoke(null, "SuspectManager Missing");
             return;
        }
        
        StartCoroutine(PostScenarioRequest(callback));
    }

    private IEnumerator PostScenarioRequest(ScenarioCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        // Convert lists to strings for the prompt
        string maleIdsJoined = suspectManager.maleVoiceIds.Count > 0 ? string.Join(", ", suspectManager.maleVoiceIds) : "Achird";
        string femaleIdsJoined = suspectManager.femaleVoiceIds.Count > 0 ? string.Join(", ", suspectManager.femaleVoiceIds) : "Achernar";

        string maleModelIndicesStr = suspectManager.maleModelIndices != null && suspectManager.maleModelIndices.Count > 0 ? string.Join(", ", suspectManager.maleModelIndices) : "0";
        string femaleModelIndicesStr = suspectManager.femaleModelIndices != null && suspectManager.femaleModelIndices.Count > 0 ? string.Join(", ", suspectManager.femaleModelIndices) : "0";

        // UPDATED SYSTEM PROMPT: Integrates Voice ID assignment logic
        string systemPrompt = $@"You are a master mystery writer. Generate a murder mystery scenario in JSON.
        
        LOGIC RULES (The Triple-Filter):
        1. Exactly 5 suspects.
        2. The Liars (No Alibi): Exactly 2 suspects have has_no_alibi = true (Killer + Red Herring).
        3. The Motivated: Exactly 2 to 3 suspects have has_motive = true (Must include the Killer).
        4. The Capable (Access): Exactly 2 to 3 suspects have has_access_to_weapon = true (Must include the Killer).
            - CRITICAL: For these suspects, you MUST provide a 'access_to_weapon_description' explaining HOW they reached/obtained the weapon.
        5. THE KILLER: The ONLY suspect with all three flags true.
        
        STORY & TIMELINE RULES:
        6. TIMELINE: You MUST provide a specific 'murder_time', 'murder_date', and 'interrogation_date'.
        7. Victim Biography: Provide 2-3 sentences of background info.
        8. Relationship: Define a clear connection.
        9. Personality: Adjectives only.
        10. RANDOMIZATION: You MUST randomize the index of the killer in the suspects list (0-4).
        11. SOLVABILITY: Every 'Motive', 'Access', and 'False Alibi' of one suspect MUST have a corresponding clue in the 'rumors' of a different suspect.
        12. CONTRADICTION: Every 'rumor' a suspect knows, must not contradict their own alibi. Unless the suspect who knows this 'rumor' has a false alibi (Killer or Red Herring).
        13. VOICE ASSIGNMENT: Assign a 'voice_id' to each suspect based on their gender.
            - Available Male IDs: [{maleIdsJoined}]
            - Available Female IDs: [{femaleIdsJoined}]
            - Available Female IDs: [{femaleIdsJoined}]
            - RULE: Try to ensure each suspect has a unique voice_id. If a list is too short, you may reuse IDs, but prioritize variety across the 5 suspects.
        14. MODEL ASSIGNMENT:
            - Available Male Model IDs: [ {maleModelIndicesStr} ]
            - Available Female Model IDs: [ {femaleModelIndicesStr} ]
            - Assign a 'model_id' (integer) to each suspect from the appropriate list based on their gender.
            - RULE: Try to assign a unique model_id for each suspect if possible.
        15. GENDER ASSIGNMENT: Assign a 'gender' ('Male' or 'Female') to each suspect. Ensure it matches the voice_id, model_id, and name.

        JSON_STRUCTURE_EXAMPLE:
        {{
          ""victim_name"": ""Name"",
          ""victim_occupation"": ""Job"",
          ""victim_biography"": ""Context..."",
          ""murder_time"": ""10:15 PM"",
          ""murder_date"": ""July 14th"",
          ""interrogation_date"": ""July 15th"",
          ""victim_discovery_details"": ""Details"",
          ""murder_weapon"": ""Weapon"",
          ""murder_location"": ""Location"",
          ""suspects"": [
            {{
              ""name"": ""Name"",
              ""gender"": ""Male"",
              ""relationship"": ""Connection"",
              ""personality"": ""Trait"",
              ""voice_id"": ""TheSelectedID"",
              ""model_id"": 0,
              ""motive"": ""Reason or null"",
              ""access_to_weapon_description"": ""Description or null"",
              ""alibi_statement"": ""Statement..."",
              ""minor_secret"": ""Secret or null"",
              ""rumors"": {{ ""OtherSuspectName"": ""Rumor text..."" }},
              ""has_no_alibi"": true,
              ""has_motive"": true,
              ""has_access_to_weapon"": true,
              ""is_killer"": true
            }}
          ]
        }}";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = "Generate a new mystery scenario following all timeline, logic, and voice assignment rules. Randomize the killer." } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload));
        using (request)
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var res = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                    string jsonText = res.candidates[0].content.parts[0].text;
                    currentScenario = JsonConvert.DeserializeObject<ScenarioData>(jsonText);

                    if (debugDisplayField != null) debugDisplayField.text = currentScenario.ToString();
                    if (notebookManager != null) notebookManager.PopulateVictimPage();

                    callback?.Invoke(currentScenario, null);
                }
                catch (Exception ex) { callback?.Invoke(null, "Parsing Error: " + ex.Message); }
            }
            else { callback?.Invoke(null, "API Error: " + request.error); }
        }
        if (currentRequest == request) currentRequest = null;
    }

    // --- CALL 2: INTERROGATION ---
    public void AnalyzeSuspectReaction(string question, SuspectData suspect, ReactionCallback callback)
    {
        StartCoroutine(PostReactionRequest(question, suspect, callback));
    }

    private IEnumerator PostReactionRequest(string question, SuspectData suspect, ReactionCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";
        
        string systemPrompt = $@"You are the emotional core of {suspect.name}.
        Analyze the incoming question and determine your immediate emotional reaction and stress level change.
        
        CONTEXT:
        Personality: {suspect.personality}
        Motive: {(suspect.has_motive ? suspect.motive : "None")}
        Guilt: {(suspect.is_killer ? "Guilty" : "Innocent")}
        
        QUESTION: ""{question}""
        
        Possible Emotions: Neutral, Angry, Shocked, Sad, Smug, Nervous, Guilty
        
        Output JSON:
        {{
            ""emotion"": ""EmotionType"",
            ""stress_change"": 0.1  // Value between -0.2 (calming down) and 0.5 (very stressful)
        }}";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = "Analyze reaction." } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload));
        using (request)
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var res = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                    var reaction = JsonConvert.DeserializeObject<ReactionResult>(res.candidates[0].content.parts[0].text);
                    
                    if (Enum.TryParse<FaceAnimator.EmotionType>(reaction.emotion, true, out FaceAnimator.EmotionType emotionEnum))
                    {
                        callback?.Invoke(emotionEnum, reaction.stress_change, null);
                    }
                    else
                    {
                        callback?.Invoke(FaceAnimator.EmotionType.Neutral, 0f, "Failed to parse emotion");
                    }
                }
                catch (Exception ex) { callback?.Invoke(FaceAnimator.EmotionType.Neutral, 0f, ex.Message); }
            }
            else { callback?.Invoke(FaceAnimator.EmotionType.Neutral, 0f, request.error); }
        }
        if (currentRequest == request) currentRequest = null;
    }

    public void SpeakWithSuspect(string question, SuspectData suspect, InterrogationCallback callback)
    {
        StartCoroutine(PostInterrogationRequest(question, suspect, callback));
    }

    private IEnumerator PostInterrogationRequest(string question, SuspectData suspect, InterrogationCallback callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";

        string timelineContext = $"TODAY IS: {currentScenario.interrogation_date}. THE MURDER HAPPENED ON: {currentScenario.murder_date} AT {currentScenario.murder_time}.";

        string systemPrompt = $@"You are roleplaying as {suspect.name}.
        {timelineContext}
        RELATIONSHIP: {suspect.relationship}.
        TRAITS: {suspect.personality}.
        ALIBI: {suspect.alibi_statement}.
        MOTIVE: {(suspect.has_motive ? suspect.motive : "None.")}.
        ACCESS: {(suspect.has_access_to_weapon ? suspect.access_to_weapon_description : "None.")}.
        GUILT: {(suspect.is_killer ? "YOU ARE THE KILLER." : "INNOCENT.")}.
        RUMORS YOU KNOW: {JsonConvert.SerializeObject(suspect.rumors)}.
        RULES: 
        1. Stay in character. 
        2. 1-2 sentences. 
        3. Refer only to what your character knows as described in the JSON file
        4. Do not use any colon symbols when referring to time, but say things like 3 45PM as to keep TTS models capable of repeating your response properly
        5. When asked about things that do not at all relate to the case, point out the absurdidy of the question
        6. When pressured about their false alibi, only the suspect with no motive and a false alibi (= Red Herring) will reveal their minor secret, explaining why they would fake their alibi
        
        Output JSON:
        {{
            ""response"": ""Your verifiable in-character response string."",
            ""end_emotion"": ""EmotionType (Neutral, Angry, Shocked, Sad, Smug, Nervous, Guilty)"",
            ""stress_change"": 0.0, // Optional refinement
            ""requires_thinking"": true // Boolean: TRUE if the question is hard/complex/requires memory. FALSE if easy/immediate.
        }}";

        suspect.chatHistory.Add(new GeminiContent { role = "user", parts = new List<GeminiPart> { new GeminiPart { text = question } } });

        var payload = new
        {
            contents = suspect.chatHistory.ToArray(),
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload));
        using (request)
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var res = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                var jsonText = res.candidates[0].content.parts[0].text;
                var suspectResponse = JsonConvert.DeserializeObject<SuspectResponse>(jsonText);
                
                suspect.chatHistory.Add(new GeminiContent { role = "model", parts = new List<GeminiPart> { new GeminiPart { text = suspectResponse.response } } });
                callback?.Invoke(suspectResponse, null);
            }
            else { callback?.Invoke(null, request.error); }
        }
        if (currentRequest == request) currentRequest = null;
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

        string systemPrompt = $@"You are a cynical 1940s crime journalist for the 'Daily Truth'. 
        Compare the detective's accusation to the hidden truth.
        
        TRUTH: {JsonConvert.SerializeObject(currentScenario)}
        
        TASK:
        1. Evaluate if the detective identified the CORRECT KILLER and valid LOGIC (Motive/Access).
        2. Write a SENSATIONAL NEWSPAPER HEADLINE. 
           - If Correct: Celebrate the capture.
           - If Incorrect: Reveal the TRUE KILLER in the headline (e.g., 'DETECTIVE BLUNDERS! [True Killer] WAS THE REAL CULPRIT!').
        3. Write the ARTICLE BODY (Noir Style).
           - Provide feedback on the investigation.
           - Explain WHY the logic was right or wrong.
           - If the detective missed clues, mention them mockingly or tragically.
        
        Output JSON: 
        - is_correct (boolean)
        - headline (string)
        - article (string)";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = $"Accused: {accusedName}\nMotive: {motive}\nAccess: {access}" } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new { 
                        is_correct = new { type = "BOOLEAN" }, 
                        headline = new { type = "STRING" },
                        article = new { type = "STRING" }
                    },
                    required = new[] { "is_correct", "headline", "article" }
                }
            }
        };

        UnityWebRequest request = CreateRequest(url, JsonConvert.SerializeObject(payload));
        using (request)
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var res = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                var result = JsonConvert.DeserializeObject<JudgeResult>(res.candidates[0].content.parts[0].text);
                callback?.Invoke(result.headline, result.article, result.is_correct, null);
            }
            else { callback?.Invoke(null, null, false, request.error); }
        }
        if (currentRequest == request) currentRequest = null;
    }

    // Track current request
    private UnityWebRequest currentRequest;

    /// <summary>
    /// Cancels any active logical interaction (Reaction or Speaking).
    /// </summary>
    public void CancelCurrentInteraction()
    {
        if (currentRequest != null)
        {
            currentRequest.Abort();
            currentRequest.Dispose();
            currentRequest = null;
        }
        StopAllCoroutines();
    }

    private UnityWebRequest CreateRequest(string url, string json)
    {
        UnityWebRequest req = new UnityWebRequest(url, "POST");
        currentRequest = req; // Track request
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
    [Serializable] private class JudgeResult { public bool is_correct; public string headline; public string article; }
    [Serializable] private class ReactionResult { public string emotion; public float stress_change; }
    [Serializable] public class SuspectResponse { public string response; public string end_emotion; public float stress_change; public bool requires_thinking; }
}