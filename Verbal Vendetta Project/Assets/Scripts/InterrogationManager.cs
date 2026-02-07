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

    [Header("Interrogation State")]
    public int currentSuspectIndex = 0;

    private void Start()
    {
        if (newsArticle != null) newsArticle.SetActive(false);

        suspectNameDisplay.text = "Generating scenario...";
        responseTextField.text = "<i>Please wait while the mystery is prepared...</i>";

        if (connectionManager != null)
        {
            connectionManager.GenerateScenario((data, error) =>
            {
                if (data != null)
                {
                    UpdateSuspectUI();
                }
                else
                {
                    suspectNameDisplay.text = "Generation Failed";
                    responseTextField.text = $"<color=red>Error:</color> {error}";
                }
            });
        }
    }

    public void SwitchSuspectUpwards()
    {
        if (connectionManager.currentScenario == null) return;
        currentSuspectIndex = (currentSuspectIndex + 1) % connectionManager.currentScenario.suspects.Count;
        UpdateSuspectUI();
    }

    private void UpdateSuspectUI()
    {
        if (connectionManager.currentScenario == null) return;

        SuspectData currentSuspect = connectionManager.currentScenario.suspects[currentSuspectIndex];
        suspectNameDisplay.text = $"Interrogating: {currentSuspect.name}";
        responseTextField.text = $"<i>{currentSuspect.name} waits for your first question.</i>";
    }

    public void AskSuspect()
    {
        if (connectionManager.currentScenario == null) return;

        string question = playerInputField.text;
        if (string.IsNullOrWhiteSpace(question)) return;

        responseTextField.text = "<i>Thinking...</i>";
        SuspectData activeSuspect = connectionManager.currentScenario.suspects[currentSuspectIndex];
        // Append the player's question to the suspect's transcript in the notebook
        if (notebookManager != null)
        {
            notebookManager.AppendSuspectLine(currentSuspectIndex, $"Player: {question}");
        }
        // Disable TTS trigger until we have a response
        connectionManager.SpeakWithSuspect(question, activeSuspect, (response, error) =>
        {
            if (string.IsNullOrEmpty(error))
            {
                responseTextField.text = $"<b>{activeSuspect.name}:</b> {response}";
                playerInputField.text = "";

                // Only play TTS if we actually received text
                if (!string.IsNullOrEmpty(response) && ttsHandler != null && !string.IsNullOrEmpty(activeSuspect.voice_id))
                {
                    ttsHandler.PlayVoice(response, activeSuspect.voice_id);
                }

                // Append the suspect's response to the notebook transcript
                if (notebookManager != null)
                {
                    notebookManager.AppendSuspectLine(currentSuspectIndex, $"{activeSuspect.name}: {response}");
                }

                // Inform any input manager that an answer was received so that UI state can be reset
                var inputMgr = FindObjectOfType<InterrogationInputManager>();
                if (inputMgr != null) inputMgr.OnAnswerReceived();
            }
            else
            {
                responseTextField.text = $"<color=red>Error:</color> {error}";
            }
        });
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
    
    private IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(60f);
        if (scenesManager != null) scenesManager.GoToMenu();
    }
}