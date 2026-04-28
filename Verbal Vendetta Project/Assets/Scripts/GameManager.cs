using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    public enum GameState { SubjectSelection, Interrogation, Ending, Accusation }
    public GameState currentState = GameState.SubjectSelection;

    [Header("Dependencies")]
    public InterrogationManager interrogationManager;
    public SelectionManager selectionManager;
    public SuspectManager suspectManager;
    public GeminiConnectionManager connectionManager;
    public InterrogationInputManager inputManager; // Added reference
    public Camera mainCamera;

    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public TMP_Text loadingText;
    public GameObject accusationButton;

    public void ShowLoadingScreen(string message)
    {
        if (loadingScreen != null) loadingScreen.SetActive(true);
        if (loadingText != null) loadingText.text = message;
    }

    public void HideLoadingScreen()
    {
        if (loadingScreen != null) loadingScreen.SetActive(false);
        if (loadingText != null) loadingText.text = "";
    }

    [Header("Interrogation Scene")]
    public Transform interrogationSpot;
    public Transform interrogationCameraPos;

    // Internal State
    private GameObject currentActiveHighDetailModel;
    private bool isInputLocked = false;

    void Start()
    {
        if (inputManager == null) inputManager = FindObjectOfType<InterrogationInputManager>();
        if (accusationButton != null) accusationButton.SetActive(currentState == GameState.SubjectSelection);

        // Initial Setup - Camera to Selection
        if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
        {
            mainCamera.transform.position = selectionManager.cameraPosition.position;
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation * Quaternion.Euler(0, 180, 0);
        }

        // Wait for Generation, then Spawn Lineup
        if (connectionManager != null)
        {
             ShowLoadingScreen("Generating Scenario...");
             connectionManager.GenerateScenario(async (data, error) =>
             {
                 if (data != null)
                 {
                     // Delegate Spawning to SelectionManager
                     if (selectionManager != null)
                     {
                         selectionManager.SpawnLineup(data);
                         selectionManager.isInputActive = true;
                     }

                     if (interrogationManager != null)
                     {
                         interrogationManager.SetActiveSuspect(null, null);
                     }

                     // Generate Briefing Audio for Notebook Page 1
                     if (interrogationManager != null && interrogationManager.conversationPipeline != null && interrogationManager.notebookManager != null)
                     {
                         ShowLoadingScreen("Synthesizing Briefing...");
                         await GenerateBriefing(data);
                     }

                     HideLoadingScreen();
                 }
                 else
                 {
                     HideLoadingScreen();
                     Debug.LogError("Generation Failed: " + error);
                 }
             });
        }
    }

    private async Task GenerateBriefing(ScenarioData data)
    {
        Debug.Log("[GameManager] Starting Briefing Synthesis...");
        AudioClip briefing = await interrogationManager.conversationPipeline.GenerateBriefingAudio(data);
        
        if (briefing != null && interrogationManager.notebookManager != null)
        {
            Debug.Log($"[GameManager] Briefing synthesized successfully. Length: {briefing.length}s");
            interrogationManager.notebookManager.briefingClip = briefing;

            // Automatic fallback if no AudioSource is assigned
            if (interrogationManager.notebookManager.briefingAudioSource == null)
            {
                AudioSource kokoroSource = interrogationManager.conversationPipeline.kokoroManager.GetComponent<AudioSource>();
                interrogationManager.notebookManager.briefingAudioSource = kokoroSource;
                Debug.Log("[GameManager] Assigned KokoroManager AudioSource as fallback for Notebook briefing.");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] Briefing synthesis failed or NotebookManager is missing.");
        }
    }

    void Update()
    {
        // Microphone Mute Toggle
        if (Input.GetKeyDown(KeyCode.M) && currentState == GameState.Interrogation)
        {
            if (inputManager != null) inputManager.ToggleMute();
        }

        if (isInputLocked) return;

        if (currentState == GameState.SubjectSelection)
        {
            if (selectionManager != null) selectionManager.HandleInput();
            
            // Check for Toggle
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Check if Notebook is open - if so, disable mode switching
                if (interrogationManager != null && 
                    interrogationManager.notebookManager != null && 
                    interrogationManager.notebookManager.IsOpen)
                {
                    return; 
                }

                StartCoroutine(SwitchToInterrogation());
            }

            // Continuous UI Update (Optional, can be event based)
            if (selectionManager != null && interrogationManager != null && interrogationManager.suspectNameDisplay != null)
            {
                SuspectData data = selectionManager.GetSelectedSuspectData();
                if (data != null) interrogationManager.suspectNameDisplay.text = $"Selected: {data.name}";
            }

            // ENTER to start Accusation Phase
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartAccusationPhase();
            }
        }
        else if (currentState == GameState.Interrogation)
        {
            // Press Space to go back
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Check if Notebook is open - if so, disable mode switching
                if (interrogationManager != null && 
                    interrogationManager.notebookManager != null && 
                    interrogationManager.notebookManager.IsOpen)
                {
                    return; 
                }

                StartCoroutine(SwitchToSelection());
            }
        }
    }

    private System.Collections.IEnumerator SwitchToInterrogation()
    {
        if (selectionManager == null) yield break;
        inputManager.micImage.gameObject.SetActive(true);
        isInputLocked = true;
        currentState = GameState.Interrogation;

        // 1. Get Data from Selection Manager
        SuspectData activeData = selectionManager.GetSelectedSuspectData();
        int modelId = activeData.model_id;

        // 2. Hide Lineup
        selectionManager.isInputActive = false;
        selectionManager.SetVisible(false);

        // 3. Move Camera
        if (mainCamera != null && interrogationCameraPos != null)
        {
            mainCamera.transform.position = interrogationCameraPos.position;
            mainCamera.transform.rotation = interrogationCameraPos.rotation * Quaternion.Euler(0, 90, 0);
        }

        // 4. Spawn High Detail Model at Interrogation Spot
        if (currentActiveHighDetailModel != null) Destroy(currentActiveHighDetailModel);

        if (suspectManager != null)
        {
            // Use SuspectManager to spawn with offsets
            currentActiveHighDetailModel = suspectManager.SpawnSuspect(modelId, interrogationSpot);
            
            if (currentActiveHighDetailModel != null)
            {
                // 5. Inform InterrogationManager
                interrogationManager.SetActiveSuspect(activeData, currentActiveHighDetailModel);
                
                // Randomize floor point for new interrogation
                if (EyePointManager.Instance != null)
                {
                    EyePointManager.Instance.RandomizeFloorPoint();
                }
            }
        }

        if (accusationButton != null) accusationButton.SetActive(false);

        yield return new WaitForSeconds(0.5f); 
        isInputLocked = false;
    }

    private System.Collections.IEnumerator SwitchToSelection()
    {
        // 0. Cancel any pending input
        if (inputManager != null)
        {
            inputManager.ForceReset();
        }
        inputManager.micImage.gameObject.SetActive(false);
        isInputLocked = true;
        currentState = GameState.SubjectSelection;

        // 1. Destroy High Detail Model
        if (currentActiveHighDetailModel != null)
        {
            Destroy(currentActiveHighDetailModel);
            currentActiveHighDetailModel = null;
        }

        // 2. Move Camera Back
        if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
        {
            mainCamera.transform.position = selectionManager.cameraPosition.position;
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation * Quaternion.Euler(0, 180, 0);
        }

        // 3. Show Lineup
        if (selectionManager != null)
        {
            selectionManager.SetVisible(true);
            selectionManager.isInputActive = true;
        }
        
        // Reset Interrogation UI text?
        if (interrogationManager != null)
        {
            interrogationManager.StopInterrogation(); // Cancel All Processes
            interrogationManager.SetActiveSuspect(null, null); // Clear active suspect
        }

        if (accusationButton != null) accusationButton.SetActive(true);

        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }

    public void StartAccusationPhase()
    {
        if (isInputLocked) return;
        StartCoroutine(SwitchToAccusation());
    }

    private System.Collections.IEnumerator SwitchToAccusation()
    {
        if (inputManager != null) inputManager.ForceReset();
        
        isInputLocked = true;
        currentState = GameState.Accusation;
        inputManager.micImage.gameObject.SetActive(true);
        
        if (selectionManager != null)
        {
            selectionManager.isInputActive = false;
            // selectionManager.SetVisible(false); // KEEP VISIBLE
        }

        if (currentActiveHighDetailModel != null)
        {
            Destroy(currentActiveHighDetailModel);
            currentActiveHighDetailModel = null;
        }
        
        if (interrogationManager != null)
        {
            interrogationManager.StopInterrogation();
            interrogationManager.PrepareAccusationUI();
            
            if (interrogationManager.conversationPipeline != null)
            {
                interrogationManager.conversationPipeline.ConnectSession(null, true);
            }
        }

        /* REMOVED CAMERA SWITCH
        if (mainCamera != null && interrogationCameraPos != null)
        {
            mainCamera.transform.position = interrogationCameraPos.position;
            mainCamera.transform.rotation = interrogationCameraPos.rotation * Quaternion.Euler(0, 90, 0);
        }
        */

        if (accusationButton != null) accusationButton.SetActive(false);

        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }
}
