using UnityEngine;
using TMPro;
using System;

/// <summary>
/// Manages Microphone input and Text input fallback.
/// Flow: Type/Record -> Transcribe/Submit -> Show Result -> Send.
/// </summary>
public class InterrogationInputManager : MonoBehaviour
{
    [Header("Dependencies")]
    public InterrogationManager interrogationManager;
    public ConversationPipeline conversationPipeline;
    public GameManager gameManager;

    [Header("UI Feedback")]
    public UnityEngine.UI.Image micImage;
    public Sprite micOnSprite;
    public Sprite micOffSprite;
    public TMP_InputField textInputFallback;
    public TMP_Text debugTranscriptionText;

    private bool isRecording = false;

    void Start()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        // Register the submission event
        if (textInputFallback != null)
        {
            textInputFallback.onEndEdit.AddListener(HandleSubmit);
        }
    }

    void Update()
    {
        // 1. Block all input while pause menu is open
        if (PauseMenuManager.IsPaused) return;

        // 2. Manage Visibility based on Game State + PlayerPrefs
        if (textInputFallback != null && gameManager != null)
        {
            bool textInputPref = PlayerPrefs.GetInt("TextInputEnabled", 1) == 1;
            bool shouldBeVisible = textInputPref &&
                (gameManager.currentState == GameManager.GameState.Interrogation ||
                 gameManager.currentState == GameManager.GameState.Accusation);
            if (textInputFallback.gameObject.activeSelf != shouldBeVisible)
            {
                textInputFallback.gameObject.SetActive(shouldBeVisible);
                if (!shouldBeVisible && textInputFallback.isFocused)
                {
                    textInputFallback.DeactivateInputField();
                }
            }
        }

        // 3. Check Valid States for Input
        if (gameManager == null || (gameManager.currentState != GameManager.GameState.Interrogation && 
                                    gameManager.currentState != GameManager.GameState.Accusation)) return;

        // Block input if Pin Board is open (unless in Accusation)
        if (PinBoardManager.Instance != null && PinBoardManager.Instance.IsOpen && 
            gameManager.currentState != GameManager.GameState.Accusation) return;

        bool isTyping = textInputFallback != null && textInputFallback.isFocused;

        // 4. Handle Auto-Focus (Opening the text box)
        if (textInputFallback != null && !isTyping && Input.anyKeyDown)
        {
            // Only focus if the key isn't a "system" or "action" key
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2) &&
                !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return) && 
                !Input.GetKeyDown(KeyCode.KeypadEnter) && !Input.GetKeyDown(KeyCode.Escape) && 
                !Input.GetKeyDown(KeyCode.Tab))
            {
                textInputFallback.ActivateInputField();
                // We don't manually append characters; TMP handles this once activated.
            }
        }

        // 5. Handle Hold-to-Talk (Only if not typing)
        if (!isTyping)
        {
            // Allow spacebar only during Accusation Phase
            if (gameManager != null && gameManager.currentState == GameManager.GameState.Accusation)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StartRecording();
                }
                else if (Input.GetKeyUp(KeyCode.Space))
                {
                    StopRecording();
                }
            }
        }

        // 6. Mic UI Feedback
        UpdateMicUI();
    }

    /// <summary>
    /// Event triggered by TMP_InputField when Enter is pressed.
    /// </summary>
    private void HandleSubmit(string text)
    {
        // We only proceed if the user actually hit Enter (not just clicked away)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                conversationPipeline?.SubmitTextQuestion(text);
            }

            // Clear the text and kill focus so the placeholder resets
            textInputFallback.text = "";
            textInputFallback.DeactivateInputField();
        }
    }

    private void UpdateMicUI()
    {
        if (micImage == null) return;

        if (isRecording)
        {
            if (micOnSprite != null) micImage.sprite = micOnSprite;
            micImage.color = Color.red;
        }
        else
        {
            if (micOffSprite != null) micImage.sprite = micOffSprite;
            micImage.color = Color.white;
        }
    }

    private void OnEnable()
    {
        if (conversationPipeline != null)
            conversationPipeline.OnTranscriptionReceived += HandleTranscriptionReceived;
    }

    private void OnDisable()
    {
        if (conversationPipeline != null)
            conversationPipeline.OnTranscriptionReceived -= HandleTranscriptionReceived;
        
        if (textInputFallback != null)
            textInputFallback.onEndEdit.RemoveListener(HandleSubmit);
    }

    private void HandleTranscriptionReceived(string speaker, string text)
    {
        if (speaker == "Player" && debugTranscriptionText != null)
        {
            debugTranscriptionText.text = $"Last Heard: \"{text}\"";
        }
    }

    public void StartRecording()
    {
        isRecording = true;
        conversationPipeline?.StartRecording();
    }

    public void StopRecording()
    {
        isRecording = false;
        conversationPipeline?.StopRecording();
    }

    public void OnAnswerReceived() { /* State handled by Update */ }


    public void ToggleMute()
    {
        if (conversationPipeline != null)
        {
            // This assumes your ConversationPipeline has a ToggleMute or IsMuted property.
            // If it doesn't, you can simply leave this empty to stop the error, 
            // or implement your own mute logic here.
            Debug.Log("ToggleMute called.");
        }
    }
    public void ForceReset()
    {
        if (isRecording) StopRecording();
        isRecording = false;
        if (textInputFallback != null) textInputFallback.text = "";
        UpdateMicUI();
    }

    /// <summary>
    /// Called by SettingsManager when the TextInputEnabled preference changes.
    /// Forces an immediate visibility refresh of the text input field.
    /// </summary>
    public void RefreshTextInputVisibility()
    {
        if (textInputFallback == null || gameManager == null) return;

        bool textInputPref = PlayerPrefs.GetInt("TextInputEnabled", 1) == 1;
        bool shouldBeVisible = textInputPref &&
            (gameManager.currentState == GameManager.GameState.Interrogation ||
             gameManager.currentState == GameManager.GameState.Accusation);

        textInputFallback.gameObject.SetActive(shouldBeVisible);
        if (!shouldBeVisible && textInputFallback.isFocused)
        {
            textInputFallback.DeactivateInputField();
        }
    }
}