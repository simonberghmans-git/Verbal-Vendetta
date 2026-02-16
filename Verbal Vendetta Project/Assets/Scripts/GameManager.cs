using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum GameState { SubjectSelection, Interrogation }
    public GameState currentState = GameState.SubjectSelection;

    [Header("Dependencies")]
    public InterrogationManager interrogationManager;
    public SelectionManager selectionManager;
    public SuspectManager suspectManager;
    public GeminiConnectionManager connectionManager;
    public Camera mainCamera;

    [Header("Interrogation Scene")]
    public Transform interrogationSpot;
    public Transform interrogationCameraPos;

    // Internal State
    private GameObject currentActiveHighDetailModel;
    private bool isInputLocked = false;

    void Start()
    {
        // Initial Setup - Camera to Selection
        if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
        {
            mainCamera.transform.position = selectionManager.cameraPosition.position;
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation;
        }

        // Wait for Generation, then Spawn Lineup
        if (connectionManager != null)
        {
             connectionManager.GenerateScenario((data, error) =>
             {
                 if (data != null)
                 {
                     // Delegate Spawning to SelectionManager
                     if (selectionManager != null)
                     {
                         selectionManager.SpawnLineup(data);
                         selectionManager.isInputActive = true;
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
        if (isInputLocked) return;

        if (currentState == GameState.SubjectSelection)
        {
            if (selectionManager != null) selectionManager.HandleInput();
            
            // Check for Toggle
            if (Input.GetKeyDown(KeyCode.I))
            {
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
            // Press I to go back
            if (Input.GetKeyDown(KeyCode.I))
            {
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
            mainCamera.transform.rotation = interrogationCameraPos.rotation;
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
            }
        }

        yield return new WaitForSeconds(0.5f); 
        isInputLocked = false;
    }

    private System.Collections.IEnumerator SwitchToSelection()
    {
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
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation;
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
            interrogationManager.SetActiveSuspect(null, null); // Clear active suspect
        }

        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }
}
