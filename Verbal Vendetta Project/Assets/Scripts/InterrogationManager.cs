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
    public GeminiTTSHandler ttsHandler; // Updated to Gemini TTS
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

    private SuspectData activeSuspectData;
    private GameObject currentSuspectModel;

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
        }

        // Register Animator
        if (currentSuspectModel != null && AnimationsManager.Instance != null)
        {
            AnimationsManager.Instance.stressLevel = 0f;
            AnimationsManager.Instance.SetCurrentAnimator(currentSuspectModel.GetComponent<Animator>());
        }
    }

    // Removed Internal UpdateSuspectUI and UpdateSuspectModel as they are handled by GameManager now.

    public void AskSuspect()
    {
        currentFullResponse = "";
        activeSuspectIndex = -1;

        if (connectionManager.currentScenario == null) return;

        string question = playerInputField.text;
        if (string.IsNullOrWhiteSpace(question)) return;

        if (activeSuspectData == null) return;
        responseTextField.text = "<i>Thinking...</i>";
        SuspectData activeSuspect = activeSuspectData;
        
        // Switch Eye State to Wandering (Thinking)
        if (EyePointManager.Instance != null)
        {
            EyePointManager.Instance.currentState = EyePointManager.EyeState.Thinking;
        }

        // Append the player's question to the suspect's transcript
        if (notebookManager != null)
        {
            // Note: We might want to pass the correct ID if notebook manager uses index. 
            // For now, assuming appending by subject object reference isn't possible, let's just append to current page 
            // or we need to pass the index to SetActiveSuspect if NotebookManager relies on it.
            // Let's check NotebookManager usage below. 
            // It uses `currentSuspectIndex`. We should probably keep that updated or overload notebook manager.
            // For safety, let's rely on the activeSuspect object.
            // Actually, we can just find the index of activeSuspectData in the scenario list.
            int index = connectionManager.currentScenario.suspects.IndexOf(activeSuspect);
            notebookManager.AppendSuspectLine(index, $"Player: {question}");
        }

        // STEP 1: Immediate Reaction Analysis
        connectionManager.AnalyzeSuspectReaction(question, activeSuspect, (reactionEmotion, stressChange, reactionError) => 
        {
            if (reactionError == null)
            {
                // Immediate Transition: Apply the reaction emotion NOW
                if (AnimationsManager.Instance != null)
                {
                    AnimationsManager.Instance.stressLevel = Mathf.Clamp01(AnimationsManager.Instance.stressLevel + stressChange);
                    
                    // We need to access the current animator's FaceAnimator component
                    // Assuming the currentSuspectModel has the FaceAnimator
                    if (currentSuspectModel != null)
                    {
                        var faceAnim = currentSuspectModel.GetComponent<FaceAnimator>();
                        if (faceAnim != null)
                        {
                            faceAnim.SetEmotion(reactionEmotion);
                        }
                    }
                }
            }
        
            // STEP 2: Get Verbal Response
            connectionManager.SpeakWithSuspect(question, activeSuspect, (suspectResponse, error) =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    responseTextField.text = $"<b>{activeSuspect.name}:</b> {suspectResponse.response}";
                    playerInputField.text = "";

                    // Parse End Emotion
                    FaceAnimator.EmotionType endEmotion = FaceAnimator.ParseEmotion(suspectResponse.end_emotion);
                    FaceAnimator.EmotionType startEmotion = reactionEmotion; // We are currently at this emotion

                    // Apply Eye Contact Logic
                    if (EyePointManager.Instance != null)
                    {
                        // If requires_thinking is TRUE, we do NOT force direct eye contact (allow wandering/floor looking)
                        // If requires_thinking is FALSE (easy), we FORCE direct eye contact
                        EyePointManager.Instance.forceDirectEyeContact = !suspectResponse.requires_thinking;
                    }

                    // Only play TTS if we actually received text
                    if (!string.IsNullOrEmpty(suspectResponse.response) && ttsHandler != null && !string.IsNullOrEmpty(activeSuspect.voice_id))
                    {
                        AudioSource suspectAudioSource = null;
                        if (currentSuspectModel != null)
                        {
                            suspectAudioSource = currentSuspectModel.GetComponentInChildren<AudioSource>();
                        }

                        // STEP 3: Play TTS and Animate Speech Transition
                        ttsHandler.PlayVoice(suspectResponse.response, activeSuspect.voice_id, suspectAudioSource, (clip, ttsError) => 
                        {
                            if (clip != null)
                            {
                                if (currentSuspectModel != null)
                                {
                                    var faceAnim = currentSuspectModel.GetComponent<FaceAnimator>();
                                    if (faceAnim != null)
                                    {
                                        faceAnim.PlaySpeechEmotions(startEmotion, endEmotion, clip.length);
                                    }
                                }

                                if (AnimationsManager.Instance != null)
                                {
                                    AnimationsManager.Instance.SetTalkingState(true, clip.length);
                                }

                                // Set Eye State to Talking
                                if (EyePointManager.Instance != null)
                                {
                                    EyePointManager.Instance.currentState = EyePointManager.EyeState.Talking;
                                }

                                // Reset Eye State after speech
                                StartCoroutine(ResetEyeStateAfterDelay(clip.length));
                                
                                // Start delayed transcript coroutine
                                if (speechCompletionCoroutine != null) StopCoroutine(speechCompletionCoroutine);
                                speechCompletionCoroutine = StartCoroutine(WaitForSpeechCompletion(clip.length));
                            }
                            else
                            {
                                // If TTS fails, reset eyes immediately or after a short delay
                                if (EyePointManager.Instance != null)
                                    EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
                            }
                        });
                    }
                    else
                    {
                         // No TTS, reset eyes immediately
                         if (EyePointManager.Instance != null)
                            EyePointManager.Instance.currentState = EyePointManager.EyeState.Waiting;
                    }

                    // Append the suspect's response to the notebook transcript
                    if (notebookManager != null)
                    {
                        activeSuspectIndex = connectionManager.currentScenario.suspects.IndexOf(activeSuspect); // Track index
                        currentFullResponse = suspectResponse.response; // Track full text
                        // DELAYED transcript update: Wait for speech to finish or be interrupted
                        // notebookManager.AppendSuspectLine(...); // REMOVED
                    }

                    // Inform any input manager that an answer was received
                    var inputMgr = FindObjectOfType<InterrogationInputManager>();
                    if (inputMgr != null) inputMgr.OnAnswerReceived();
                }
                else
                {
                    responseTextField.text = $"<color=red>Error:</color> {error}";
                }
            });
        });
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

    // Track current speech for interruption cutoff
    private string currentFullResponse = "";
    private int activeSuspectIndex = -1;
    private Coroutine speechCompletionCoroutine;

    /// <summary>
    /// Cancel all ongoing interrogation processes (STT, LLM, TTS).
    /// </summary>
    public void StopInterrogation()
    {
        // 0. Handle Transcript Cutoff
        // If we have a pending speech completion, we must finalize the transcript now.
        if (speechCompletionCoroutine != null)
        {
            StopCoroutine(speechCompletionCoroutine);
            string textToPost = currentFullResponse;

            // If still speaking, calculate cutoff. 
            // If not speaking (finished?), default to full text (textToPost).
            if (ttsHandler != null && ttsHandler.IsSpeaking && !string.IsNullOrEmpty(currentFullResponse))
            {
                float percentage = ttsHandler.GetPlaybackPercentage();
                
                // Calculate cutoff index based on percentage of text length
                int cutoffIndex = Mathf.FloorToInt(currentFullResponse.Length * percentage);
                cutoffIndex = Mathf.Clamp(cutoffIndex, 0, currentFullResponse.Length);
                
                // Create cutoff string
                textToPost = currentFullResponse.Substring(0, cutoffIndex) + " ... [INTERRUPTED]";
            }
            
            if (!string.IsNullOrEmpty(textToPost))
            {
                FinalizeTranscript(textToPost);
            }
        }

        // 1. Cancel LLM Generation
        if (connectionManager != null)
        {
            connectionManager.CancelCurrentInteraction();
        }

        // 2. Stop TTS Playback and Generation
        if (ttsHandler != null)
        {
            ttsHandler.StopSpeaking();
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
        }

        // 5. Reset UI
        responseTextField.text = "<i>...</i>";
        playerInputField.text = "";
        // suspectNameDisplay.text = "Select a Suspect"; // Handled by SetActiveSuspect(null) in GM
        
        currentFullResponse = "";
        activeSuspectIndex = -1;
        speechCompletionCoroutine = null;
    }

    private void FinalizeTranscript(string textToAppend)
    {
        if (notebookManager != null && activeSuspectIndex != -1)
        {
            notebookManager.AppendSuspectLine(activeSuspectIndex, $"{activeSuspectData.name}: {textToAppend}");
        }
    }

    private IEnumerator WaitForSpeechCompletion(float duration)
    {
        yield return new WaitForSeconds(duration);
        FinalizeTranscript(currentFullResponse);
        speechCompletionCoroutine = null;
        
        // Also handling Eye State reset here if needed, but the original code had its own coroutine 'ResetEyeStateAfterDelay'
        // We can consolidate or keep separate. The existing 'ResetEyeStateAfterDelay' is fine for eyes.
    }
    
    private IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(60f);
        if (scenesManager != null) scenesManager.GoToMenu();
    }
}