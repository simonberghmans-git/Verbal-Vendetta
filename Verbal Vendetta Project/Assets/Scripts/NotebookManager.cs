using System.Collections.Generic;
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

    [Header("Transcripts")]
    [Tooltip("TMP_Text fields for each suspect's full transcript. Order must match suspects in the scenario.")]
    public List<TMP_Text> suspectTranscriptTexts = new List<TMP_Text>();
    [Tooltip("Separate TMP_Text fields for each suspect's name/header. Order must match suspects in the scenario.")]
    public List<TMP_Text> suspectHeaderTexts = new List<TMP_Text>();

    [Header("Notebook Pages")]
    [Tooltip("Root GameObject for the Notebook UI. This will be toggled with Tab.")]
    public GameObject notebookRoot;

    [Tooltip("Each page is a separate GameObject. Order determines paging.")]
    public List<GameObject> notebookPages = new List<GameObject>();

    [Tooltip("Index of the page that should be shown when the notebook is opened (0-based).")]
    public int startingPageIndex = 0;

    // currently active page index, -1 when none
    private int currentPageIndex = -1;

    // internal transcript storage
    private List<string> suspectTranscripts = new List<string>();

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

        // Initialize per-suspect transcripts when a new scenario is loaded
        InitializeTranscripts(data);
    }

    /// <summary>
    /// Prepare internal transcript storage and clear the UI fields. Call when a new scenario is loaded.
    /// </summary>
    public void InitializeTranscripts(ScenarioData scenario)
    {
        suspectTranscripts.Clear();
        if (scenario == null || scenario.suspects == null) return;

        int count = scenario.suspects.Count;
        for (int i = 0; i < count; i++)
        {
            suspectTranscripts.Add("");
            // Set header in separate header text if available
            if (i < suspectHeaderTexts.Count && suspectHeaderTexts[i] != null)
            {
                suspectHeaderTexts[i].text = $"<b>{scenario.suspects[i].name}</b>";
            }

            // Initialize the transcript body placeholder independently
            if (i < suspectTranscriptTexts.Count && suspectTranscriptTexts[i] != null)
            {
                suspectTranscriptTexts[i].text = "No questions asked yet";
            }
        }
    }

    /// <summary>
    /// Append a single line to the specified suspect's transcript and update the associated TMP_Text.
    /// </summary>
    public void AppendSuspectLine(int suspectIndex, string line)
    {
        if (suspectIndex < 0) return;

        // Ensure internal list is large enough
        while (suspectIndex >= suspectTranscripts.Count) suspectTranscripts.Add("");

        string toAdd = line ?? "";
        if (suspectTranscripts[suspectIndex].Length > 0)
            suspectTranscripts[suspectIndex] += "\n" + toAdd;
        else
            suspectTranscripts[suspectIndex] = toAdd;

        if (suspectIndex < suspectTranscriptTexts.Count && suspectTranscriptTexts[suspectIndex] != null)
        {
            // Transcript body is kept separate from header; show placeholder if empty
            suspectTranscriptTexts[suspectIndex].text = string.IsNullOrEmpty(suspectTranscripts[suspectIndex]) ? "No questions asked yet" : suspectTranscripts[suspectIndex];
        }
    }

    public string GetTranscript(int suspectIndex)
    {
        if (suspectIndex < 0 || suspectIndex >= suspectTranscripts.Count) return "";
        return suspectTranscripts[suspectIndex];
    }

    /// <summary>
    /// Append a formatted question and answer pair to the specified suspect's transcript.
    /// Format:
    /// You: "question"
    /// Suspect: "answer"
    /// Does not add the suspect's name to the transcript (header is kept separately).
    /// </summary>
    public void AppendQuestionAndAnswer(int suspectIndex, string question, string answer)
    {
        if (suspectIndex < 0) return;

        // Ensure internal list is large enough
        while (suspectIndex >= suspectTranscripts.Count) suspectTranscripts.Add("");

        string q = question ?? "";
        string a = answer ?? "";
        string combined = $"You: \"{q}\"\nSuspect: \"{a}\"";

        if (suspectTranscripts[suspectIndex].Length > 0)
            suspectTranscripts[suspectIndex] += "\n" + combined;
        else
            suspectTranscripts[suspectIndex] = combined;

        if (suspectIndex < suspectTranscriptTexts.Count && suspectTranscriptTexts[suspectIndex] != null)
        {
            // Transcript body is separate from header; show placeholder if empty
            suspectTranscriptTexts[suspectIndex].text = string.IsNullOrEmpty(suspectTranscripts[suspectIndex]) ? "No questions asked yet" : suspectTranscripts[suspectIndex];
        }
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

    // Initialize notebook state on start
    private void Start()
    {
        // Ensure notebook root is hidden at start
        if (notebookRoot != null)
        {
            notebookRoot.SetActive(false);
        }

        // Ensure pages are all deactivated initially
        for (int i = 0; i < notebookPages.Count; i++)
        {
            if (notebookPages[i] != null)
                notebookPages[i].SetActive(false);
        }

        currentPageIndex = -1;
    }

    // Toggle notebook visibility with Tab and allow simple page navigation
    private void Update()
    {
        // Toggle notebook with Tab
        if (Input.GetKeyDown(KeyCode.Tab) && notebookRoot != null)
        {
            bool active = notebookRoot.activeSelf;
            if (active)
                HideNotebook();
            else
                ShowNotebook();
        }
    }

    public void ShowNotebook()
    {
        if (notebookRoot == null) return;
        notebookRoot.SetActive(true);
        // always show the first page when opened
        if (notebookPages.Count > 0)
            ShowPage(0);
    }

    public void HideNotebook()
    {
        if (notebookRoot == null) return;
        notebookRoot.SetActive(false);
        // deactivate any active page
        if (currentPageIndex >= 0 && currentPageIndex < notebookPages.Count && notebookPages[currentPageIndex] != null)
            notebookPages[currentPageIndex].SetActive(false);
        currentPageIndex = -1;
    }

    public void ShowPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= notebookPages.Count) return;

        // Deactivate current
        if (currentPageIndex >= 0 && currentPageIndex < notebookPages.Count && notebookPages[currentPageIndex] != null)
            notebookPages[currentPageIndex].SetActive(false);

        // Activate new
        if (notebookPages[pageIndex] != null)
            notebookPages[pageIndex].SetActive(true);

        currentPageIndex = pageIndex;
    }

    public void NextPage()
    {
        if (notebookPages.Count == 0) return;
        int next;
        if (currentPageIndex < 0)
            next = 0;
        else
            next = (currentPageIndex + 1) % notebookPages.Count; // wrap to first after last
        ShowPage(next);
    }

    public void PreviousPage()
    {
        if (notebookPages.Count == 0) return;
        int prev;
        if (currentPageIndex < 0)
            prev = 0;
        else
            prev = (currentPageIndex - 1 + notebookPages.Count) % notebookPages.Count; // wrap to last when going back from first
        ShowPage(prev);
    }
}