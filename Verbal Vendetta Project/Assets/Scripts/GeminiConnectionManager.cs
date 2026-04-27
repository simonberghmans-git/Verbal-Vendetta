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
    public string apiKey = ""; // Made public for sync

    private string model = "gemini-flash-latest"; // Using the latest alias for maximum availability

    public ScenarioData currentScenario;

    public bool testing = false;

    private const string TEST_SCENARIO_JSON = @"
{
  ""victim_name"": ""Alistair Thorne"",
  ""victim_occupation"": ""Museum Curator"",
  ""victim_biography"": ""Alistair was a world-renowned archeologist and the director of the Thorne Museum of Antiquities. He was known for his uncompromising ethics and a massive private collection of Mesopotamian artifacts that he refused to sell to private collectors."",
  ""victim_discovery_details"": ""The body was found by the night janitor in the Hall of Relics, slumped against a glass display case that had been unlocked. A ceremonial Babylonian dagger was found nearby, wiped clean of prints."",
  ""murder_time"": ""11:30 PM"",
  ""murder_date"": ""October 12th"",
  ""interrogation_date"": ""October 13th"",
  ""murder_weapon"": ""Ceremonial Babylonian Dagger"",
  ""murder_location"": ""The Hall of Relics, Thorne Museum"",
  ""suspects"": [
    {
      ""name"": ""Julian Vane"",
      ""gender"": ""Male"",
      ""relationship"": ""Assistant Curator"",
      ""personality"": ""Nervous, meticulous, ambitious"",
      ""voice_id"": ""Algieba"",
      ""model_id"": 0,
      ""motive"": null,
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was working late in my private office on the third floor organizing the new exhibit catalog. I didn't see or hear a soul until the police arrived."",
      ""minor_secret"": ""He has been forging Alistair's signature to approve minor loan agreements."",
      ""rumors"": {
        ""Arthur Sterling"": ""I noticed Arthur Sterling's silver sedan speeding away from the museum parking lot at approximately 11:45 PM, which is odd for someone who claims to have been home."",
        ""Marcus Blackwood"": ""Marcus is far too relaxed; I often see him leaving his post for long cigarette breaks near the side entrance.""
      },
      ""has_no_alibi"": true,
      ""has_motive"": false,
      ""has_access_to_weapon"": false,
      ""is_killer"": false
    },
    {
      ""name"": ""Arthur Sterling"",
      ""gender"": ""Male"",
      ""relationship"": ""Rival Collector"",
      ""personality"": ""Arrogant, wealthy, obsessed"",
      ""voice_id"": ""Charon"",
      ""model_id"": 1,
      ""motive"": ""Alistair refused to sell him a rare Babylonian cylinder seal that would complete Arthur's billion-dollar collection."",
      ""access_to_weapon_description"": ""Snatched the security master key from the desk when the head of security was distracted, allowing him to bypass the display case locks."",
      ""alibi_statement"": ""I was at my estate all night, reading by the fire. My staff was off for the evening, so it was just me and my books."",
      ""minor_secret"": ""He is currently facing a lawsuit for purchasing looted artifacts from war zones."",
      ""rumors"": {
        ""Julian Vane"": ""Julian claims he was working, but I happen to know the security cameras on the third floor were manually disabled from the inside right before the murder.""
      },
      ""has_no_alibi"": true,
      ""has_motive"": true,
      ""has_access_to_weapon"": true,
      ""is_killer"": true
    },
    {
      ""name"": ""Elena Thorne"",
      ""gender"": ""Female"",
      ""relationship"": ""Victim's Daughter"",
      ""personality"": ""Elegant, cold, desperate"",
      ""voice_id"": ""Achernar"",
      ""model_id"": 3,
      ""motive"": ""Alistair threatened to disinherit her and cut off her allowance due to her massive, unresolved gambling debts."",
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was at the Underground Royale casino until 2:00 AM. The pit boss and several dealers can vouch for my presence there all evening."",
      ""minor_secret"": ""She has already contacted a black-market dealer to price out her father's private collection."",
      ""rumors"": {
        ""Arthur Sterling"": ""I heard Arthur shouting at my father in the office last week, saying that Alistair would 'regret his stubbornness' regarding the seal."",
        ""Marcus Blackwood"": ""Marcus is the only person who carries the master keys at all times; he's incredibly protective of them.""
      },
      ""has_no_alibi"": false,
      ""has_motive"": true,
      ""has_access_to_weapon"": false,
      ""is_killer"": false
    },
    {
      ""name"": ""Marcus Blackwood"",
      ""gender"": ""Male"",
      ""relationship"": ""Head of Security"",
      ""personality"": ""Disciplined, observant, gruff"",
      ""voice_id"": ""Alnilam"",
      ""model_id"": 2,
      ""motive"": null,
      ""access_to_weapon_description"": ""As the head of security, he possesses the only master key that opens every display case and restricted door in the museum."",
      ""alibi_statement"": ""I was performing my scheduled perimeter sweep of the grounds from 11:15 PM to 11:45 PM, which is logged in the digital security system."",
      ""minor_secret"": ""He was once dishonorably discharged from the military for unknown reasons."",
      ""rumors"": {
        ""Elena Thorne"": ""I found a stack of 'Final Notice' debt collection letters in Elena's trash while doing my rounds; she's in far deeper than she admits.""
      },
      ""has_no_alibi"": false,
      ""has_motive"": false,
      ""has_access_to_weapon"": true,
      ""is_killer"": false
    },
    {
      ""name"": ""Clara Whitby"",
      ""gender"": ""Female"",
      ""relationship"": ""Journalist"",
      ""personality"": ""Inquisitive, charming, sharp"",
      ""voice_id"": ""Kore"",
      ""model_id"": 4,
      ""motive"": null,
      ""access_to_weapon_description"": null,
      ""alibi_statement"": ""I was live-streaming the Charity Gala at the Mayor's mansion. There is timestamped video of me interviewing the guests at exactly 11:30 PM."",
      ""minor_secret"": ""She was planning an exposé on the museum's potential financial insolvency."",
      ""rumors"": {
        ""Arthur Sterling"": ""While I was researching my story near the museum lobby yesterday, I saw Arthur Sterling lingering near the security desk while Marcus was outside on a break.""
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
    // public delegate void JudgeCallback(string headline, string article, bool isCorrect, string error);
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
        Debug.Log($"Gemini Scenario Request URL: {url.Replace(apiKey, "HIDDEN_KEY")}");

        string maleModelIndicesStr = suspectManager.maleModelIndices != null && suspectManager.maleModelIndices.Count > 0 ? string.Join(", ", suspectManager.maleModelIndices) : "0";
        string femaleModelIndicesStr = suspectManager.femaleModelIndices != null && suspectManager.femaleModelIndices.Count > 0 ? string.Join(", ", suspectManager.femaleModelIndices) : "0";

        int maxMaleVoiceIndex = Mathf.Max(0, suspectManager.maleKokoroVoices.Count - 1);
        int maxFemaleVoiceIndex = Mathf.Max(0, suspectManager.femaleKokoroVoices.Count - 1);

        // UPDATED SYSTEM PROMPT: Integrates Voice Index assignment logic
        string systemPrompt = $@"You are a master mystery writer. Generate a murder mystery scenario in JSON.
        
        LOGIC RULES (The Triple-Filter):
        1. Exactly 5 suspects (3 males, 2 females).
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
        13. VOICE ASSIGNMENT: Assign a 'voice_index' (integer) to each suspect based on their gender.
            - Available Male Indices: 0 to {maxMaleVoiceIndex}
            - Available Female Indices: 0 to {maxFemaleVoiceIndex}
            - RULE: Try to ensure each suspect has a unique voice_index. If the list is too short, you may reuse indices.
        14. MODEL ASSIGNMENT:
            - Available Male Model IDs: [ {maleModelIndicesStr} ]
            - Available Female Model IDs: [ {femaleModelIndicesStr} ]
            - Assign a 'model_id' (integer) to each suspect from the appropriate list based on their gender.
            - RULE: Try to assign a unique model_id for each suspect if possible.
        15. GENDER: Ensure 'gender' ('Male'/'Female') matches voice_id and name.
        
        Return ONLY valid JSON. do not include markdown formatting.

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
              ""voice_index"": 0,
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
    public void GenerateInterrogationResponse(string playerInput, SuspectData activeSuspect, string pastTranscript, bool isPoliceChief, Action<string, string> callback)
    {
        if (currentScenario == null) return;
        StartCoroutine(PostInterrogationRequest(playerInput, activeSuspect, pastTranscript, isPoliceChief, callback));
    }

    private IEnumerator PostInterrogationRequest(string playerInput, SuspectData activeSuspect, string pastTranscript, bool isPoliceChief, Action<string, string> callback)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey.Trim()}";
        Debug.Log($"Gemini Interrogation Request URL: {url.Replace(apiKey, "HIDDEN_KEY")}");
        
        string systemPrompt;

        if (isPoliceChief)
        {
            systemPrompt = $@"You are the cynical, no-nonsense Police Chief of the precinct.
The detective (the player) is calling you to submit an official accusation.
You do not know the truth of the case, but you demand clear logic.
Respond naturally to the detective's statements. 
Limit your response to 2-3 sentences. Keep it conversational.";
        }
        else
        {
            string rumorsJson = JsonConvert.SerializeObject(activeSuspect.rumors);
            systemPrompt = $@"You are a suspect in a murder mystery being interrogated by a detective (the player).
            
        YOUR PROFILE:
        Name: {activeSuspect.name}
        Personality: {activeSuspect.personality}
        Relationship to victim: {activeSuspect.relationship}
        Alibi: {activeSuspect.alibi_statement}
        
        KNOWLEDGE:
        You know the following rumors about others: {rumorsJson}
        
        CASE DETAILS:
        Victim: {currentScenario.victim_name}
        Time/Date: {currentScenario.murder_time}, {currentScenario.murder_date}
        Weapon: {currentScenario.murder_weapon}
        
        RULES:
        1. Stay in character at all times. Do not break the fourth wall.
        2. Keep your answers brief and concise (1-3 sentences maximum).
        3. Do not over-explain unless pressed. 
        4. If you are the killer ({activeSuspect.is_killer}), you will lie about your motive and access to the weapon, and stick to your false alibi.
        5. Respond naturally to the detective's most recent statement or question.
        6. Do not generate asterisks or roleplay actions (e.g. *sighs*), only dialogue.";
        }

        string fullPrompt = $"Previous Transcript:\n{pastTranscript}\n\nDetective: {playerInput}\n{ (isPoliceChief ? "Police Chief" : activeSuspect.name) }:";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = fullPrompt } } } },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new { responseMimeType = "text/plain" }
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
                    string generatedText = res.candidates[0].content.parts[0].text;
                    callback?.Invoke(generatedText, null);
                }
                catch (Exception ex) { callback?.Invoke(null, "Parsing Error: " + ex.Message); }
            }
            else { callback?.Invoke(null, "API Error: " + request.error); }
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
        Debug.Log($"Gemini Judge Request URL: {url.Replace(apiKey, "HIDDEN_KEY")}");

        string systemPrompt = $@"You are a cynical 1940s crime journalist for the 'Daily Truth'. 
        Compare the detective's accusation to the hidden truth.
        
        TRUTH: {JsonConvert.SerializeObject(currentScenario)}
        
        TASK:
        1. Evaluate if the detective identified the CORRECT KILLER and valid LOGIC (Motive/Access).
        2. Write a SENSATIONAL NEWSPAPER HEADLINE. 
           - If Correct: Celebrate the capture.
           - If Incorrect: Reveal the TRUE KILLER in the headline (e.g., 'DETECTIVE BLUNDERS! [True Killer] WAS THE REAL CULPRIT!').
           - Make the headline no more than 45 characters long (including spaces and punctuation). Try to fit this length requirement as best as possible.
           - Do not capitalize the headline. Only the first letters of important words should be capitalized.
        3. Write the ARTICLE BODY (Noir Style).
           - Provide feedback on the investigation.
           - Explain WHY the logic was right or wrong.
           - If the detective missed clues, mention them mockingly or tragically.
           - Make the article body no more than 685 characters long (including spaces and punctuation). Try to fit this length requirement as best as possible. 
        3. Write everything as if it is directed towards a casual newsreader, do not refer to the detective as 'you'.
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
                responseMimeType = "application/json"
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
    // [Serializable] private class JudgeResult { public bool is_correct; public string headline; public string article; }
}