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
    public ConversationPipeline conversationPipeline; // Replaces GeminiLiveConnection
    public PinBoardManager pinBoardManager;
    public ScenesManager scenesManager;
    public GameManager gameManager; // Need this to access GameState

    public AudioClip accusationTriggerClip;

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


    public void RecordLastStatement()
    {
        if (activeSuspectData != null && !string.IsNullOrEmpty(currentModelTranscript))
        {
            if (pinBoardManager != null)
            {
                pinBoardManager.AddEvidenceScrap(currentModelTranscript);
            }
            else if (PinBoardManager.Instance != null)
            {
                PinBoardManager.Instance.AddEvidenceScrap(currentModelTranscript);
            }
        }
    }

    public void SetActiveSuspect(SuspectData data, GameObject suspectObject)
    {
        activeSuspectData = data;
        currentSuspectModel = suspectObject;

        // Clear existing transcript cards on the desk when switching suspects
        TranscriptCardManager cardManager = FindObjectOfType<TranscriptCardManager>();
        if (cardManager != null)
        {
            cardManager.ClearAllCards();
        }

        // Reset Eye State to Direct (Idle)
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
            EyePointManager.Instance.forceDirectEyeContact = false;
        }

        // Register Animator and AudioSource for LipSync
        if (currentSuspectModel != null)
        {
            if (AnimationsManager.Instance != null)
            {
                AnimationsManager.Instance.stressLevel = 0f;
                AnimationsManager.Instance.SetCurrentAnimator(currentSuspectModel.GetComponent<Animator>());
            }

            if (conversationPipeline != null && conversationPipeline.geminiRemoteTTSManager != null)
            {
                AudioSource suspectAudio = currentSuspectModel.GetComponent<AudioSource>();
                if (suspectAudio == null) suspectAudio = currentSuspectModel.GetComponentInChildren<AudioSource>();
                conversationPipeline.geminiRemoteTTSManager.SetTargetAudioSource(suspectAudio);
            }
        }

        // Initialize Local Pipeline Connection
        if (conversationPipeline != null && activeSuspectData != null)
        {
            conversationPipeline.ConnectSession(activeSuspectData, false);
            
            // Unsubscribe just in case, then subscribe to events
            conversationPipeline.OnTranscriptionReceived -= HandleTranscription;
            conversationPipeline.OnMetadataReceived -= HandleMetadata;
            conversationPipeline.OnSpeakStateChanged -= HandleSpeakStateChanged;
            
            conversationPipeline.OnTranscriptionReceived += HandleTranscription;
            conversationPipeline.OnMetadataReceived += HandleMetadata;
            conversationPipeline.OnSpeakStateChanged += HandleSpeakStateChanged;
        }
    }

    // Removed Internal UpdateSuspectUI and UpdateSuspectModel as they are handled by GameManager now.

    public void AskSuspect()
    {
        // Now handled via ConversationPipeline.
        // InterrogationInputManager will call conversationPipeline.StartRecording() / StopRecording().
    }

    private void HandleTranscription(string speaker, string text)
    {
        if (speaker != "Player")
        {
            // Model
            currentModelTranscript = text;
        }
        
        // Note: NotebookManager logic removed in favor of PinBoard 'R' key recording.
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

    public void PrepareAccusationUI()
    {
        // UI logic removed
    }

    /// <summary>
    /// Submits the final accusation report to the Judge API automatically from the AI conversation.
    /// </summary>
    public void SubmitAutomatedAccusation(string name, string motive, string access)
    {
        if (connectionManager.currentScenario == null) return;

        // STOP interrogation right away so the suspect doesn't keep talking
        StopInterrogation();

        // Inform GameManager
        if (gameManager != null)
        {
            gameManager.currentState = GameManager.GameState.Ending;
        }

        if (gameManager != null) gameManager.ShowLoadingScreen("Finalizing Police Report...");

        connectionManager.JudgeAccusation(name, motive, access, async (headline, article, isCorrect, error) =>
        {
            AudioClip newsClip = null;
            if (string.IsNullOrEmpty(error) && conversationPipeline != null)
            {
                if (gameManager != null) gameManager.ShowLoadingScreen("Synthesizing News Broadcast...");
                newsClip = await conversationPipeline.GenerateNewsAudio(article);
            }

            if (gameManager != null) gameManager.HideLoadingScreen();
            ProcessJudgeResult(headline, article, isCorrect, error, newsClip);
        });
    }



    private void ProcessJudgeResult(string headline, string article, bool isCorrect, string error, AudioClip newsClip = null)
    {
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

            // Play News Audio via KokoroManager's AudioSource
            if (newsClip != null && conversationPipeline != null && conversationPipeline.geminiRemoteTTSManager != null)
            {
                AudioSource geminiSource = conversationPipeline.geminiRemoteTTSManager.GetComponent<AudioSource>();
                
                // Stop any existing speech (e.g., Police Chief)
                if (geminiSource.isPlaying) geminiSource.Stop();
                
                // Ensure we are playing from the local source, not a suspect's source
                conversationPipeline.geminiRemoteTTSManager.SetTargetAudioSource(null); 
                
                geminiSource.clip = newsClip;
                geminiSource.Play();
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
    }

    // Track current speech for transition states
    // (Legacy tracking removed)

    public void StopInterrogation()
    {
        // Handle interruption marker in transcript
        // Note: NotebookManager logic removed.
        isModelSpeaking = false;
        currentModelTranscript = "";

        // Cancel LLM Generation and TTS pipeline
        if (conversationPipeline != null)
        {
            conversationPipeline.OnTranscriptionReceived -= HandleTranscription;
            conversationPipeline.OnMetadataReceived -= HandleMetadata;
            conversationPipeline.OnSpeakStateChanged -= HandleSpeakStateChanged;
            conversationPipeline.DisconnectSession();
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

        // Wait for the news audio to finish if it's still playing
        if (conversationPipeline != null && conversationPipeline.geminiRemoteTTSManager != null)
        {
            AudioSource geminiSource = conversationPipeline.geminiRemoteTTSManager.GetComponent<AudioSource>();
            if (geminiSource != null && geminiSource.isPlaying)
            {
                while (geminiSource.isPlaying)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(1f); // Brief pause after audio ends
            }
        }

        if (scenesManager != null) scenesManager.GoToMenu();
    }
}