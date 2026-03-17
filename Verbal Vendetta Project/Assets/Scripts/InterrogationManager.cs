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
    public GameManager gameManager; // Need this to access GameState

    [Header("Interrogation UI")]
    public TMP_InputField playerInputField;
    public TMP_Text responseTextField;
    public TMP_Text suspectNameDisplay;

    [Header("Accusation UI")]
    public TMP_InputField accusedNameInput;
    public TMP_InputField motiveInput;
    public TMP_InputField accessInput;

    [Header("End Game Newspaper UI")]
    public TMP_Text articleHeadlineDisplay;
    public TMP_Text articleBodyDisplay;
    public TMP_Text articleBodyDisplay2;
    public UnityEngine.UI.Image killerPortraitDisplay;
    public GameObject newsArticle;

    [Header("End Game Camera Logic")]
    public Transform endPosition1;
    public Transform endPosition2;
    public float cameraLerpDuration; // Time before returning to menu
    public GameObject mainUICanvas; // The canvas housing crosshair/notebook that gets disabled

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
        suspectNameDisplay.text = "Select a Suspect";
        responseTextField.text = "<i>Press 'Space' or use Arrows to select.</i>";
        
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
            responseTextField.text = "<i>Press 'Space' or use Arrows to select.</i>";
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
        
        if (notebookManager != null && activeSuspectData != null)
        {
            int index = connectionManager.currentScenario.suspects.IndexOf(activeSuspectData);
            if (speaker == "Player")
            {
                notebookManager.AppendSuspectLine(index, $"Player: {text}");
            }
            else
            {
                notebookManager.AppendSuspectLine(index, text);
            }
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
            if (articleBodyDisplay != null)
                articleBodyDisplay.text = "Error: No scenario loaded.";
            return;
        }

        string name = accusedNameInput.text;
        string motive = motiveInput.text;
        string access = accessInput.text;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(motive) || string.IsNullOrWhiteSpace(access))
        {
            if (articleBodyDisplay != null)
                articleBodyDisplay.text = "Please fill in all fields of the report.";
            return;
        }

        // STOP interrogation right away so the suspect doesn't keep talking
        StopInterrogation();

        // Inform GameManager
        if (gameManager != null)
        {
            gameManager.currentState = GameManager.GameState.Ending;
        }

        if (gameManager != null) gameManager.ShowLoadingScreen("Submitting Accusation...");

        connectionManager.JudgeAccusation(name, motive, access, (headline, article, isCorrect, error) =>
        {
            if (gameManager != null) gameManager.HideLoadingScreen();

            if (string.IsNullOrEmpty(error))
            {
                if (articleHeadlineDisplay != null)
                {
                    articleHeadlineDisplay.text = headline;
                }
                
                if (articleBodyDisplay != null)
                {
                    if (article.Length <= 306)
                    {
                        articleBodyDisplay.text = article;
                        if (articleBodyDisplay2 != null) articleBodyDisplay2.text = "";
                    }
                    else
                    {
                        int breakPoint = 306;
                        
                        // Backtrack to the nearest whitespace to ensure no word is cut in half
                        while (breakPoint > 0 && !char.IsWhiteSpace(article[breakPoint]))
                        {
                            breakPoint--;
                        }

                        if (breakPoint == 0) 
                        {
                            breakPoint = 306; // Fallback
                        }
                        
                        articleBodyDisplay.text = article.Substring(0, breakPoint).Trim();
                        if (articleBodyDisplay2 != null)
                        {
                            articleBodyDisplay2.text = article.Substring(breakPoint).Trim();
                        }
                    }
                }

                // Show killer's portrait
                if (killerPortraitDisplay != null && connectionManager.currentScenario != null)
                {
                    SuspectManager suspectManager = FindObjectOfType<SuspectManager>();
                    if (suspectManager != null)
                    {
                        var killer = connectionManager.currentScenario.suspects.Find(s => s.is_killer);
                        if (killer != null)
                        {
                            killerPortraitDisplay.sprite = suspectManager.GetSuspectImage(killer.model_id);
                            killerPortraitDisplay.enabled = true;
                        }
                    }
                }

                // Hide the main UI canvas to remove crosshair/notebook availability
                if (mainUICanvas != null)
                {
                    mainUICanvas.SetActive(false);
                }

                StartCoroutine(ReturnToMenuRoutine());
            }
            else
            {
                if (articleHeadlineDisplay != null)
                    articleHeadlineDisplay.text = "Newsroom Error";
                
                if (articleBodyDisplay != null)
                    articleBodyDisplay.text = $"<color=red>Error:</color> {error}";
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
        if (gameManager != null && gameManager.mainCamera != null && 
            endPosition1 != null && endPosition2 != null && newsArticle != null)
        {
            Transform camTransform = gameManager.mainCamera.transform;
            camTransform.position = endPosition1.position;
            camTransform.LookAt(newsArticle.transform, newsArticle.transform.up);

            Vector3 startPos = endPosition1.position;
            float elapsed = 0f;

            // Slow lerp across the newspaper
            while (elapsed < cameraLerpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / cameraLerpDuration;
                camTransform.position = Vector3.Lerp(startPos, endPosition2.position, t);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(cameraLerpDuration);
        }

        if (scenesManager != null) scenesManager.GoToMenu();
    }
}