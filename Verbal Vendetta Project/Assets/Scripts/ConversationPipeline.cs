using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class ConversationPipeline : MonoBehaviour
{
    [Header("Dependencies")]
    public LocalWhisperManager whisperManager;
    public GeminiConnectionManager geminiManager;
    public KokoroManager kokoroManager;
    public SuspectManager suspectManager;
    
    [Header("State")]
    public SuspectData activeSuspect;
    public bool isPoliceChiefMode = false;
    public bool sttTestMode = false; // Added for testing STT separately
    
    // UI Events (Mimicking the old LiveConnection)
    public event Action<string, string> OnTranscriptionReceived; // speaker, text
    public event Action<bool> OnSpeakStateChanged; // isSpeaking
    public event Action<string, string, float> OnMetadataReceived; // startEmotion, endEmotion, stress
    
    private string pastTranscript = "";

    private void OnEnable()
    {
        if (whisperManager != null)
        {
            whisperManager.OnTranscriptionReceived += HandlePlayerWhisperTranscription;
        }
        if (kokoroManager != null)
        {
            kokoroManager.OnSpeechFinished += HandleKokoroFinished;
        }
    }

    private void OnDisable()
    {
        if (whisperManager != null)
        {
            whisperManager.OnTranscriptionReceived -= HandlePlayerWhisperTranscription;
        }
        if (kokoroManager != null)
        {
            kokoroManager.OnSpeechFinished -= HandleKokoroFinished;
        }
    }

    public void ConnectSession(SuspectData suspect, bool policeChief = false)
    {
        activeSuspect = suspect;
        isPoliceChiefMode = policeChief;
        pastTranscript = "";
        
        // Assign Voice based on suspect or police chief
        if (kokoroManager != null && suspectManager != null)
        {
            string assignedVoice;
            
            if (isPoliceChiefMode)
            {
                assignedVoice = suspectManager.policeChiefVoice;
            }
            else if (activeSuspect != null)
            {
                if (activeSuspect.gender == "Male")
                {
                    int index = Mathf.Clamp(activeSuspect.voice_index, 0, suspectManager.maleKokoroVoices.Count - 1);
                    assignedVoice = suspectManager.maleKokoroVoices[index];
                }
                else
                {
                    int index = Mathf.Clamp(activeSuspect.voice_index, 0, suspectManager.femaleKokoroVoices.Count - 1);
                    assignedVoice = suspectManager.femaleKokoroVoices[index];
                }
            }
            else
            {
                return;
            }

            kokoroManager.SetVoice(assignedVoice);
        }
    }

    public void DisconnectSession()
    {
        activeSuspect = null;
        isPoliceChiefMode = false;
        geminiManager?.CancelCurrentInteraction();
    }

    public void StartRecording()
    {
        whisperManager?.StartRecording();
    }

    public void StopRecording()
    {
        whisperManager?.StopRecording();
    }

    private void HandlePlayerWhisperTranscription(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        OnTranscriptionReceived?.Invoke("Player", text);
        
        // Add to transcript
        pastTranscript += $"Detective: {text}\n";

        // Call Gemini
        if (geminiManager != null && !sttTestMode)
        {
            geminiManager.GenerateInterrogationResponse(text, activeSuspect, pastTranscript, isPoliceChiefMode, HandleGeminiResponse);
        }
    }

    private void HandleGeminiResponse(string responseText, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"Gemini Error: {error}");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(responseText)) return;

        // Parse JSON result
        GeminiConnectionManager.InterrogationResult result = geminiManager.SafeDeserialize<GeminiConnectionManager.InterrogationResult>(responseText);

        if (result == null || string.IsNullOrEmpty(result.text))
        {
            Debug.LogError($"Parsed Gemini result is invalid. Raw: {responseText}");
            return;
        }

        string speakerName = isPoliceChiefMode ? "Police Chief" : (activeSuspect != null ? activeSuspect.name : "Character");
        
        // Append to transcript
        pastTranscript += $"{speakerName}: {result.text}\n";

        // Clean up response for UI and TTS (remove action asterisks if Gemini accidentally generates them)
        string cleanedText = Regex.Replace(result.text, @"\*.*?\*", "").Trim();
        
        OnTranscriptionReceived?.Invoke(speakerName, cleanedText);

        // Trigger Metadata (Emotions)
        string emotion = string.IsNullOrEmpty(result.emotion) ? "Neutral" : result.emotion;
        OnMetadataReceived?.Invoke(emotion, emotion, 0.5f);

        // Start Kokoro
        if (kokoroManager != null)
        {
            OnSpeakStateChanged?.Invoke(true);
            kokoroManager.SynthesizeAndPlay(cleanedText);
        }
    }

    public async Task<AudioClip> GenerateBriefingAudio(ScenarioData data)
    {
        if (data == null || kokoroManager == null || suspectManager == null) return null;

        // Set the Police Chief voice first
        kokoroManager.SetVoice(suspectManager.policeChiefVoice);

        string briefingText = $"Listen up detective, we've got a new case on our hands. The victim is {data.victim_name}, a {data.victim_occupation}. " +
                            $"Body was found at {data.murder_location}. Time of death is estimated at {data.murder_time} on {data.murder_date}. " +
                            $"The murder weapon was a {data.murder_weapon}. {data.victim_discovery_details}. " +
                            $"We've brought in {data.suspects.Count} suspects for you to talk to. Get to work.";

        return await kokoroManager.Synthesize(briefingText);
    }

    private void HandleKokoroFinished()
    {
        OnSpeakStateChanged?.Invoke(false);
    }
}
