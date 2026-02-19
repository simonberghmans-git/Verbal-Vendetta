using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Manages Microphone input and the 2-second cancellation window.
/// Flow: Record -> Transcribe -> Show Result -> Countdown -> Send.
/// Uses New Input System for Space/Esc/X keys.
/// </summary>
public class InterrogationInputManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GeminiSTTHandler sttHandler;
    public InterrogationManager interrogationManager;
    public GameManager gameManager; // Added reference

    [Header("UI Feedback")]
    public TMP_Text statusLabel;
    public TMP_Text transcriptionPreview;

    [Header("Settings")]
    public float cancellationTime = 3.0f; //set in inspector
    private string micName;
    private AudioClip recordedClip;
    private bool isRecording = false;
    private bool isTranscribing = false;
    private bool isReviewing = false;
    private Coroutine submissionCoroutine;
    private string pendingTranscript = "";

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("No microphone detected!");
        }
    }

    void Update()
    {
        // 0. check game state
        if (gameManager != null && gameManager.currentState != GameManager.GameState.Interrogation) return;

        // Check if Notebook is open - if so, disable voice input
        if (interrogationManager != null && 
            interrogationManager.notebookManager != null && 
            interrogationManager.notebookManager.IsOpen)
        {
            return;
        }

        // 1. Press and Hold Space to record (Allows Interruption)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // INTERRUPTION LOGIC:
            // If we are currently doing anything else (reviewing, transcribing, or AI is speaking/thinking),
            // we must cancel it immediately to start a new recording.
            
            if (isRecording || isReviewing || isTranscribing)
            {
                ForceReset();
            }

            // Also cancel any external AI processes (TTS, LLM generation)
            if (interrogationManager != null)
            {
                interrogationManager.StopInterrogation();
            }

            StartRecording();
        }

        // Release Space to stop recording and begin transcription
        if (Input.GetKeyUp(KeyCode.Space) && isRecording)
        {
            StopRecording();
        }

        // 2. Handle Cancellation during Review (Esc or X)
        if (isReviewing && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X)))
        {
            CancelSubmission();
        }
    }

    void StartRecording()
    {
        isRecording = true;
        // 20 second buffer, 44.1kHz
        recordedClip = Microphone.Start(micName, false, 20, 44100);
        statusLabel.text = "<color=red>RECORDING...</color>";
        transcriptionPreview.text = "...";
    }

    void StopRecording()
    {
        isRecording = false;
        int micPos = Microphone.GetPosition(micName);
        Microphone.End(micName);

        if (micPos > 0)
        {
            statusLabel.text = "TRANSCRIBING...";
            AudioClip trimmed = TrimClip(recordedClip, micPos);
            byte[] wavData = WavUtility.FromAudioClip(trimmed);
            // Step 2: Request the full transcription from Gemini
            isTranscribing = true;
            sttHandler.TranscribeAudio(wavData, (text, error) => {
                isTranscribing = false;
                if (string.IsNullOrEmpty(error))
                {
                    pendingTranscript = text;
                    transcriptionPreview.text = $"<i>\"{text}\"</i>";

                    if (!string.IsNullOrEmpty(text))
                    {
                        // Step 3: Begin the cancellation countdown only after text is visible
                        submissionCoroutine = StartCoroutine(SubmissionWindow());
                    }
                    else
                    {
                        statusLabel.text = "No speech detected.";
                        transcriptionPreview.text = "Ready.";
                    }
                }
                else
                {
                    statusLabel.text = "<color=red>STT API Error</color>";
                    transcriptionPreview.text = "Ready.";
                    pendingTranscript = "";
                }
            });
        }
    }

    IEnumerator SubmissionWindow()
    {
        isReviewing = true;
        float timer = cancellationTime;

        while (timer > 0)
        {
            statusLabel.text = $"SENDING IN {timer:F1}s... (Esc to Cancel)";
            timer -= Time.deltaTime;
            yield return null;
        }

        // Step 4: Finalize and send to Interrogation Manager
        isReviewing = false;
        statusLabel.text = "WAITING FOR RESPONSE...";
        interrogationManager.playerInputField.text = pendingTranscript;
        interrogationManager.AskSuspect();
    }

    /// <summary>
    /// Called by the InterrogationManager once the AI response has been received.
    /// Resets the UI status to allow for the next question.
    /// </summary>
    public void OnAnswerReceived()
    {
        statusLabel.text = "READY";
    }

    void CancelSubmission()
    {
        if (submissionCoroutine != null) StopCoroutine(submissionCoroutine);
        submissionCoroutine = null;
        isReviewing = false;
        statusLabel.text = "SUBMISSION CANCELLED";
        transcriptionPreview.text = "Ready.";
        pendingTranscript = "";
    }

    /// <summary>
    /// Forcefully resets the input manager state. 
    /// Stops recording, ignores pending transcriptions, and cancels submission.
    /// </summary>
    public void ForceReset()
    {
        if (isRecording)
        {
            StopRecording(); // Stop the mic
        }
        
        isRecording = false;
        
        // Cancel actual API request
        if (sttHandler != null)
        {
            sttHandler.CancelTranscription();
        }
        
        isTranscribing = false; 

        CancelSubmission();
        
        statusLabel.text = "READY";
        transcriptionPreview.text = "Ready.";
    }

    private AudioClip TrimClip(AudioClip source, int lengthSamples)
    {
        AudioClip trimmed = AudioClip.Create("InterrogationCapture", lengthSamples, source.channels, source.frequency, false);
        float[] data = new float[lengthSamples * source.channels];
        source.GetData(data, 0);
        trimmed.SetData(data, 0);
        return trimmed;
    }
}