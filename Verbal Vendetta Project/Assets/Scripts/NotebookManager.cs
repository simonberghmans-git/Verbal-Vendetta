using UnityEngine;
using TMPro;

/// <summary>
/// Manages the physical "Case File" UI elements. 
/// Responsible for populating the first page of the notebook with victim, timeline, and crime scene data.
/// </summary>
public class NotebookManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GeminiConnectionManager connectionManager;

    [Header("Page 1: The Victim File")]
    [SerializeField] private TMP_Text victimNameText;
    [SerializeField] private TMP_Text occupationText;
    [SerializeField] private TMP_Text biographyText;

    [Header("Timeline Info")]
    [SerializeField] private TMP_Text murderTimeDateText;    // Combined Time and Date of the crime
    [SerializeField] private TMP_Text interrogationDateText; // The "Now" for the investigation

    [Header("Location & Method")]
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text discoveryDetailsText;

    /// <summary>
    /// Populates the UI fields with the data from the current scenario.
    /// This should be called from your generation callback in the InterrogationManager.
    /// </summary>
    public void PopulateVictimPage()
    {
        if (connectionManager == null || connectionManager.currentScenario == null)
        {
            Debug.LogWarning("NotebookManager: No scenario data found to display.");
            return;
        }

        ScenarioData data = connectionManager.currentScenario;

        // Populate identity and bio
        if (victimNameText) victimNameText.text = "<b>Victim:</b> " + data.victim_name;
        if (occupationText) occupationText.text = "<b>Occupation:</b> " + data.victim_occupation;
        if (biographyText) biographyText.text = "<b>Biography:</b> " + data.victim_biography;

        // Populate Timeline (Murder Time + Date)
        if (murderTimeDateText)
            murderTimeDateText.text = $"<b>Time of Death:</b> {data.murder_time}, {data.murder_date}";

        // Populate Interrogation Date
        if (interrogationDateText)
            interrogationDateText.text = "<b>Current Date:</b> " + data.interrogation_date;

        // Populate location and weapon
        if (locationText) locationText.text = "<b>Location:</b> " + data.murder_location;
        if (weaponText) weaponText.text = "<b>Weapon:</b> " + data.murder_weapon;
        if (discoveryDetailsText) discoveryDetailsText.text = "<b>Details:</b> " + data.victim_discovery_details;

        Debug.Log("Notebook Page 1 updated with new timeline information.");
    }

    /// <summary>
    /// Resets the UI fields to their default state for a fresh game start.
    /// </summary>
    public void ClearPage()
    {
        if (victimNameText) victimNameText.text = "---";
        if (occupationText) occupationText.text = "---";
        if (biographyText) biographyText.text = "Awaiting briefing data...";
        if (murderTimeDateText) murderTimeDateText.text = "---";
        if (interrogationDateText) interrogationDateText.text = "---";
        if (locationText) locationText.text = "---";
        if (weaponText) weaponText.text = "---";
        if (discoveryDetailsText) discoveryDetailsText.text = "---";
    }
}