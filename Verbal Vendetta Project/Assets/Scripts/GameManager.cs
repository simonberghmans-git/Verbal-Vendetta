using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum GameState { SubjectSelection, Interrogation, Ending}
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
             connectionManager.GenerateScenario((data, error) =>
             {
                 HideLoadingScreen();
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
                 }
                 else
                 {
                     Debug.LogError("Generation Failed: " + error);
                 }
             });
        }
    }

    void Update()
    {
        // Microphone Mute Toggle
        if (Input.GetKeyDown(KeyCode.M))
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

        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }
}
