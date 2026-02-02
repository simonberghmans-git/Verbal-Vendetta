using UnityEngine;
using TMPro; // Required for TMP_InputField and TMP_Text
using System.Collections.Generic;

/// <summary>
/// Manages the UI interrogation flow. 
/// Handles switching suspects, sending questions, and submitting the final accusation.
/// </summary>
public class InterrogationManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GeminiConnectionManager connectionManager;

    [Header("Interrogation UI")]
    public TMP_InputField playerInputField; // Where the player types questions
    public TMP_Text responseTextField;      // Where the suspect's answer appears
    public TMP_Text suspectNameDisplay;     // Shows who you are currently talking to

    [Header("Accusation UI")]
    public TMP_InputField accusedNameInput;   // Input for the killer's name
    public TMP_InputField motiveInput;        // Input for the motive reasoning
    public TMP_InputField accessInput;        // Input for the access reasoning
    public TMP_Text accusationResultDisplay;  // Where the Judge's verdict appears

    [Header("Debug Display")]
    [Tooltip("Assign a TMP_Text here to see the full secret scenario JSON for testing.")]
    public TMP_Text debugScenarioDisplay;

    [Header("Interrogation State")]
    public int currentSuspectIndex = 0; // 0 to 4 (Total 5 suspects)

    private void Start()
    {
        // Display a loading message immediately so the player knows the AI is working
        suspectNameDisplay.text = "Generating scenario...";
        responseTextField.text = "<i>Please wait while the mystery is prepared...</i>";

        if (debugScenarioDisplay != null)
        {
            debugScenarioDisplay.text = "Awaiting scenario generation...";
        }

        // Automatically trigger scenario generation on startup
        if (connectionManager != null)
        {
            connectionManager.GenerateScenario((data, error) =>
            {
                if (data != null)
                {
                    // Update the debug field with the full "Master Truth" JSON
                    if (debugScenarioDisplay != null)
                    {
                        debugScenarioDisplay.text = data.ToString();
                    }

                    // Once the scenario blueprint is received, update the UI with the first suspect
                    UpdateSuspectUI();
                }
                else
                {
                    // Handle generation errors (e.g., API key issues or rate limits)
                    suspectNameDisplay.text = "Generation Failed";
                    responseTextField.text = $"<color=red>Error:</color> {error}";

                    if (debugScenarioDisplay != null)
                    {
                        debugScenarioDisplay.text = $"<color=red>Generation Failed:</color> {error}";
                    }
                }
            });
        }
        else
        {
            Debug.LogError("InterrogationManager: GeminiConnectionManager reference is missing!");
        }
    }

    /// <summary>
    /// Switches to the next suspect in the list (1-5).
    /// Loops back to the first suspect if it goes past the last one.
    /// </summary>
    public void SwitchSuspectUpwards()
    {
        if (connectionManager.currentScenario == null) return;

        currentSuspectIndex++;

        // If we go past the 5th suspect (index 4), loop back to 0
        if (currentSuspectIndex >= connectionManager.currentScenario.suspects.Count)
        {
            currentSuspectIndex = 0;
        }

        UpdateSuspectUI();
    }

    /// <summary>
    /// Updates the UI text to reflect which suspect is currently being questioned.
    /// </summary>
    private void UpdateSuspectUI()
    {
        if (connectionManager.currentScenario == null)
        {
            suspectNameDisplay.text = "No suspects loaded.";
            return;
        }

        SuspectData currentSuspect = connectionManager.currentScenario.suspects[currentSuspectIndex];
        suspectNameDisplay.text = $"Interrogating: {currentSuspect.name}";

        // Clear the response field for the new suspect conversation
        responseTextField.text = $"<i>{currentSuspect.name} waits for your first question.</i>";
    }

    /// <summary>
    /// Takes the text from the input field and sends it to the suspect via Gemini.
    /// </summary>
    public void AskSuspect()
    {
        if (connectionManager.currentScenario == null)
        {
            responseTextField.text = "Please generate a mystery scenario first!";
            return;
        }

        string question = playerInputField.text;

        if (string.IsNullOrWhiteSpace(question))
        {
            return; // Don't send empty questions
        }

        // Show a "Thinking" indicator while waiting for the API
        responseTextField.text = "<i>Thinking...</i>";

        // Get the actual data for the active suspect
        SuspectData activeSuspect = connectionManager.currentScenario.suspects[currentSuspectIndex];

        // Call the connection manager to handle the API request
        connectionManager.SpeakWithSuspect(question, activeSuspect, (response, error) =>
        {
            if (string.IsNullOrEmpty(error))
            {
                // Display the answer in the UI
                responseTextField.text = $"<b>{activeSuspect.name}:</b> {response}";

                // Clear the input field for the next question
                playerInputField.text = "";
            }
            else
            {
                // Show the error in the display field
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