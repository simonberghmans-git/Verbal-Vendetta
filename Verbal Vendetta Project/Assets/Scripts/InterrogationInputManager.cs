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

    [Header("UI Feedback")]
    public UnityEngine.UI.Image micImage;
    public Sprite micOnSprite;
    public Sprite micOffSprite;
    public GeminiLiveConnection liveConnection;
    public GameManager gameManager; // Added reference

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
        if (liveConnection != null && liveConnection.alwaysListening && micImage != null)
        {
            if (liveConnection.isMuted)
            {
                if (micOffSprite != null) micImage.sprite = micOffSprite;
            }
            else
            {
                if (micOnSprite != null) micImage.sprite = micOnSprite;
            }
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
        // Handled automatically via Update loop showing the right sprite
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
        
        if (micImage != null && micOffSprite != null)
        {
            micImage.sprite = micOffSprite;
        }
    }

    /// <summary>
    /// Toggles the microphone mute state. Can be linked to a UI Button's OnClick event.
    /// </summary>
    public void ToggleMute()
    {
        if (liveConnection != null)
        {
            liveConnection.isMuted = !liveConnection.isMuted;
        }
    }
}