using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages the UI interrogation flow and triggers TTS playback.
/// </summary>
public class InterrogationManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GeminiConnectionManager connectionManager;
    public GeminiLiveConnection liveConnection; // Replaces TTS/STT handlers
    public NotebookManager notebookManager;
    public ScenesManager scenesManager;

    [Header("Interrogation UI")]
    public TMP_InputField playerInputField;
    public TMP_Text responseTextField;
    public TMP_Text suspectNameDisplay;

    [Header("Accusation UI")]
    public TMP_InputField accusedNameInput;
    public TMP_InputField motiveInput;
    public TMP_InputField accessInput;
    public TMP_Text accusationResultDisplay;
    public GameObject endScreen;
    public GameObject newsArticle;

    [Header("Suspect Models")]
    // Models and Images are now managed by SuspectManager

    [Header("Model Configuration")]
    // Indices are now managed by GeminiConnectionManager

    // Internal State
    private SuspectData activeSuspectData;
    private GameObject currentSuspectModel;
    private bool isModelSpeaking = false;
    private string currentModelTranscript = "";

    private void Start()
    {
        if (newsArticle != null) newsArticle.SetActive(false);

        suspectNameDisplay.text = "Initializing...";
        responseTextField.text = "<i>Please wait...</i>";
        
        // Generation is now handled by GameManager
    }

    public void SetActiveSuspect(SuspectData data, GameObject suspectObject)
    {
        activeSuspectData = data;
        currentSuspectModel = suspectObject;

        if (activeSuspectData != null)
        {
            suspectNameDisplay.text = $"Interrogating: {activeSuspectData.name}";
            responseTextField.text = $"<i>{activeSuspectData.name} enters the room.</i>";
        }
        else
        {
            suspectNameDisplay.text = "Select a Suspect";
            responseTextField.text = "<i>Press 'I' or use Arrows to select.</i>";
        }

        // Reset Eye State to Direct (Idle)
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
            EyePointManager.Instance.forceDirectEyeContact = false;
        }

        // Register Animator
        if (currentSuspectModel != null && AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.stressLevel = 0f;
            AnimationsManager.Instance.SetCurrentAnimator(currentSuspectModel.GetComponent<Animator>());
        }

        // Initialize Live Connection
        if (liveConnection != null && activeSuspectData != null)
        {
            AudioSource suspectAudioSource = null;
            if (currentSuspectModel != null)
            {
                suspectAudioSource = currentSuspectModel.GetComponentInChildren<AudioSource>();
            }

            liveConnection.ConnectSession(activeSuspectData, connectionManager.currentScenario, suspectAudioSource);
            
            // Unsubscribe just in case, then subscribe to events
            liveConnection.OnTranscriptionReceived -= HandleTranscription;
            liveConnection.OnMetadataReceived -= HandleMetadata;
            liveConnection.OnSpeakStateChanged -= HandleSpeakStateChanged;
            liveConnection.OnBodyAnimationTriggered -= HandleBodyAnimationTriggered;
            liveConnection.OnForceDirectEyeContact -= HandleForceDirectEyeContact;
            
            liveConnection.OnTranscriptionReceived += HandleTranscription;
            liveConnection.OnMetadataReceived += HandleMetadata;
            liveConnection.OnSpeakStateChanged += HandleSpeakStateChanged;
            liveConnection.OnBodyAnimationTriggered += HandleBodyAnimationTriggered;
            liveConnection.OnForceDirectEyeContact += HandleForceDirectEyeContact;
        }
    }

    // Removed Internal UpdateSuspectUI and UpdateSuspectModel as they are handled by GameManager now.

    public void AskSuspect()
    {
        // Now handled via real-time stream.
        // InterrogationInputManager will call liveConnection.StartRecording() / StopRecording().
    }

    private void HandleTranscription(string speaker, string text)
    {
        // Update UI
        if (speaker == "Player")
        {
            playerInputField.text = text;
        }
        else
        {
            // Model
            currentModelTranscript = text;
            responseTextField.text = $"<b>{speaker}:</b> {text}";
        }
        
        // Notebook transcript append
        if (notebookManager != null && activeSuspectData != null)
        {
            int index = connectionManager.currentScenario.suspects.IndexOf(activeSuspectData);
            notebookManager.AppendSuspectLine(index, $"{speaker}: {text}");
        }
    }

    private void HandleSpeakStateChanged(bool isSpeaking)
    {
        isModelSpeaking = isSpeaking;

        // Note: isSpeaking = true when the character begins responding.
        // False when they have finished playing audio.
        if (AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.SetTalkingState(isSpeaking, 0f); 
        }
        
        if (EyePointManager.Instance != null)
        {
            if (isSpeaking)
            {
                EyePointManager.Instance.currentState = EyePointManager.EyeState.Talking;
            }
            else
            {
                EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
                EyePointManager.Instance.forceDirectEyeContact = false;
            }
        }
    }

    private void HandleBodyAnimationTriggered(string animationName)
    {
        if (AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.TriggerBodyAnimation(animationName);
        }
    }

    private void HandleForceDirectEyeContact()
    {
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.forceDirectEyeContact = true;
        }
    }

    private void HandleMetadata(string startEmotionString, string endEmotionString, float stressLevel)
    {
        if (AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.stressLevel = Mathf.Clamp01(stressLevel);
        }

        if (currentSuspectModel != null)
        {
            var faceAnim = currentSuspectModel.GetComponent<FaceAnimator>();
            if (faceAnim == null) faceAnim = currentSuspectModel.GetComponentInChildren<FaceAnimator>();
            
            if (faceAnim != null)
            {
                faceAnim.SetEmotion(startEmotionString); 
            }

            var faceAnimAlt = currentSuspectModel.GetComponent<FaceAnimatorAlternative>();
            if (faceAnimAlt == null) faceAnimAlt = currentSuspectModel.GetComponentInChildren<FaceAnimatorAlternative>();
            
            if (faceAnimAlt != null)
            {
                faceAnimAlt.SetEmotion(startEmotionString);
            }
        }
    }

    private IEnumerator ResetEyeStateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
        }
    }

    /// <summary>
    /// Submits the final accusation report to the Judge API.
    /// </summary>
    public void SubmitAccusation()
    {
        if (connectionManager.currentScenario == null)
        {
            if (accusationResultDisplay != null)
                accusationResultDisplay.text = "Error: No scenario loaded.";
            return;
        }

        string name = accusedNameInput.text;
        string motive = motiveInput.text;
        string access = accessInput.text;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(motive) || string.IsNullOrWhiteSpace(access))
        {
            if (accusationResultDisplay != null)
                accusationResultDisplay.text = "Please fill in all fields of the report.";
            return;
        }

        if (endScreen != null) endScreen.SetActive(true);

        if (accusationResultDisplay != null)
            accusationResultDisplay.text = "<i>Submitting report to the Editor...</i>";

        connectionManager.JudgeAccusation(name, motive, access, (headline, article, isCorrect, error) =>
        {
            if (string.IsNullOrEmpty(error))
            {
                string color = isCorrect ? "green" : "red";
                // Formatting: Bold Headline (Larger) + Article Body
                string finalOutput = $"<size=120%><b><color={color}>{headline}</color></b></size>\n\n" +
                                     $"{article}";

                if (accusationResultDisplay != null)
                {
                    accusationResultDisplay.text = finalOutput;
                }

                if (newsArticle != null) newsArticle.SetActive(true);

                StartCoroutine(ReturnToMenuRoutine());
            }
            else
            {
                if (accusationResultDisplay != null)
                    accusationResultDisplay.text = $"<color=red>Newsroom Error:</color> {error}";
            }
        });
    }

    // Track current speech for transition states
    // (Legacy tracking removed)

    public void StopInterrogation()
    {
        // Handle interruption marker in transcript
        if (isModelSpeaking && notebookManager != null && activeSuspectData != null)
        {
            int index = connectionManager.currentScenario.suspects.IndexOf(activeSuspectData);
            notebookManager.AppendSuspectLine(index, "[INTERRUPTED]");
        }
        isModelSpeaking = false;
        currentModelTranscript = "";

        // Cancel LLM Generation and Socket Connection
        if (liveConnection != null)
        {
            liveConnection.OnTranscriptionReceived -= HandleTranscription;
            liveConnection.OnMetadataReceived -= HandleMetadata;
            liveConnection.OnSpeakStateChanged -= HandleSpeakStateChanged;
            liveConnection.OnBodyAnimationTriggered -= HandleBodyAnimationTriggered;
            liveConnection.OnForceDirectEyeContact -= HandleForceDirectEyeContact;
            _ = liveConnection.DisconnectSessionAsync();
        }

        // 3. Reset Animation State (Stop Lip Sync)
        if (AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.SetTalkingState(false, 0f);
        }

        // 4. Reset Eye State
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
            EyePointManager.Instance.forceDirectEyeContact = false;
        }

        // 5. Reset UI
        responseTextField.text = "<i>...</i>";
        playerInputField.text = "";
    }

    // Removed legacy transcript helpers
    
    private IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(60f);
        if (scenesManager != null) scenesManager.GoToMenu();
    }
}