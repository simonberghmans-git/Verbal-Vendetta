using UnityEngine;
using System.Collections.Generic;

public class SelectionManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SuspectManager suspectManager;
    
    [Header("Configuration")]
    public List<Transform> lineupSpots;
    public Transform cameraPosition;
    public GameObject selectionHighlight;

    [HideInInspector] public bool isInputActive = false;
    
    // Internal State
    private int currentSelectionIndex = 0;
    private List<GameObject> spawnedLowDetailSuspects = new List<GameObject>();
    private ScenarioData currentScenarioData;

    public void SpawnLineup(ScenarioData data)
    {
        currentScenarioData = data; // Store reference
        
        // Clear existing
        foreach (var s in spawnedLowDetailSuspects) Destroy(s);
        spawnedLowDetailSuspects.Clear();

        if (suspectManager == null) 
        {
            Debug.LogError("SuspectManager reference missing in SelectionManager!");
            return;
        }

        for (int i = 0; i < data.suspects.Count; i++)
        {
            SuspectData suspect = data.suspects[i];
            int modelId = suspect.model_id;
            
            // Spawn using SuspectManager
            if (i < lineupSpots.Count)
            {
                GameObject instance = suspectManager.SpawnLineupSuspect(modelId, lineupSpots[i]);
                if (instance != null)
                {
                    spawnedLowDetailSuspects.Add(instance);
                }
            }
        }
        
        UpdateSelectionHighlight();
        SetVisible(true);
    }

    public void HandleInput()
    {
        if (!isInputActive || spawnedLowDetailSuspects.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentSelectionIndex = (currentSelectionIndex + 1) % spawnedLowDetailSuspects.Count;
            UpdateSelectionHighlight();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentSelectionIndex--;
            if (currentSelectionIndex < 0) currentSelectionIndex = spawnedLowDetailSuspects.Count - 1;
            UpdateSelectionHighlight();
        }
    }

    private void UpdateSelectionHighlight()
    {
        if (spawnedLowDetailSuspects.Count == 0) return;

        // Move Highlight
        if (selectionHighlight != null)
        {
           selectionHighlight.transform.position = spawnedLowDetailSuspects[currentSelectionIndex].transform.position + new Vector3(0, 2, 0);
        }

        // Optional: Notify UI of name? 
        // We can expose the currently selected name for GameManager to display.
    }

    public int GetSelectedSuspectIndex()
    {
        return currentSelectionIndex; 
    }
    
    public SuspectData GetSelectedSuspectData()
    {
        if (currentScenarioData != null && currentSelectionIndex < currentScenarioData.suspects.Count)
        {
            return currentScenarioData.suspects[currentSelectionIndex];
        }
        return null;
    }

    public void SetVisible(bool visible)
    {
        foreach (var s in spawnedLowDetailSuspects)
        {
            if (s != null) s.SetActive(visible);
        }
        if (selectionHighlight != null) selectionHighlight.SetActive(visible);
        
        // Note: We don't disable the camera object itself usually, just the renderers of the suspects
    }
}
