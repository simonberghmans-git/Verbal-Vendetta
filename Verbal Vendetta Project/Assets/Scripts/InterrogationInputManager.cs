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
    public InterrogationManager interrogationManager;
    public GeminiLiveConnection liveConnection;
    public GameManager gameManager; // Added reference

    [Header("UI Feedback")]
    public TMP_Text statusLabel;
    public TMP_Text transcriptionPreview;

    [Header("Settings")]
    private bool isRecording = false;

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        // 0. check game state
        if (gameManager != null && gameManager.currentState != GameManager.GameState.Interrogation) return;

        // Check if Notebook is open
        if (interrogationManager != null && 
            interrogationManager.notebookManager != null && 
            interrogationManager.notebookManager.IsOpen)
        {
            return;
        }

        // Always-listening: Just keep status updated
        if (liveConnection != null && liveConnection.alwaysListening)
        {
            statusLabel.text = "<color=green>LISTENING...</color>";
        }
    }

    void StartRecording()
    {
        // Handled automatically by LiveConnection in Always-Listening mode
    }

    void StopRecording()
    {
        // Handled automatically by LiveConnection in Always-Listening mode
    }

    /// <summary>
    /// Called by the InterrogationManager once the AI response has been received.
    /// Resets the UI status to allow for the next question.
    /// </summary>
    public void OnAnswerReceived()
    {
        statusLabel.text = "READY";
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
        
        statusLabel.text = "READY";
        transcriptionPreview.text = "Ready.";
    }
}