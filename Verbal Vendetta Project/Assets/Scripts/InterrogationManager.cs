using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the UI interrogation flow and triggers TTS playback.
/// </summary>
public class InterrogationManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GeminiConnectionManager connectionManager;
    public ElevenLabsTTSHandler ttsHandler; // New Dependency
    public NotebookManager notebookManager;

    [Header("Interrogation UI")]
    public TMP_InputField playerInputField;
    public TMP_Text responseTextField;
    public TMP_Text suspectNameDisplay;

    [Header("Accusation UI")]
    public TMP_InputField accusedNameInput;
    public TMP_InputField motiveInput;
    public TMP_InputField accessInput;
    public TMP_Text accusationResultDisplay;

    [Header("Interrogation State")]
    public int currentSuspectIndex = 0;

    private void Start()
    {
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

        if (accusationResultDisplay != null)
            accusationResultDisplay.text = "<i>Submitting report to the Judge...</i>";

        connectionManager.JudgeAccusation(name, motive, access, (isCorrect, feedback, error) =>
        {
            if (string.IsNullOrEmpty(error))
            {
                string color = isCorrect ? "green" : "red";
                string verdict = isCorrect ? "GUILTY" : "INNOCENT / WRONG REASONING";

                if (accusationResultDisplay != null)
                {
                    accusationResultDisplay.text = $"<b>Verdict: <color={color}>{verdict}</color></b>\n\n" +
                                                 $"<i>{feedback}</i>";
                }
            }
            else
            {
                if (accusationResultDisplay != null)
                    accusationResultDisplay.text = $"<color=red>Judge Error:</color> {error}";
            }
        });
    }
}