using UnityEngine;
using TMPro;


public class ScenarioDisplayUI : MonoBehaviour
{
    public GeminiConnectionManager connectionManager;
    public TMP_Text displayField;

    public void OnButtonClick()
    {
        displayField.text = "Contacting Gemini API... Please wait.";

        connectionManager.GenerateScenario("", "", (data, error) => {
            if (data != null)
            {
                // Here we use that ToString() override we made!
                displayField.text = data.ToString();
                Debug.Log("Scenario Received and Displayed.");
            }
            else
            {
                displayField.text = "<color=red>Error:</color> " + error;
            }
        });
    }
}