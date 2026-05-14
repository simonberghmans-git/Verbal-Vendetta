using UnityEngine;
using System.Collections.Generic;

public class SelectionManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SuspectManager suspectManager;
    
    [Header("Configuration")]
    public List<Transform> lineupSpots;
    public Transform cameraPosition;
    public Light selectionLight; 
    public Vector3 lightOffset = new Vector3(0, 3, 0);

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
        
        UpdateSelectionHighlight(-1);
        SetVisible(true);
    }

    public void HandleInput()
    {
        if (!isInputActive || spawnedLowDetailSuspects.Count == 0) return;

        // --- Mouse Hover & Click Selection ---
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        bool isHovering = false;
        int hoveredIndex = -1;

        // Prevent selection if clicking on UI
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) 
        {
            // If over UI, we clear highlights but don't do selection
            UpdateSelectionHighlight(-1);
            return;
        }

        if (Physics.Raycast(ray, out hit))
        {
            // Check if we hit one of our suspects
            for (int i = 0; i < spawnedLowDetailSuspects.Count; i++)
            {
                if (hit.transform.IsChildOf(spawnedLowDetailSuspects[i].transform))
                {
                    isHovering = true;
                    hoveredIndex = i;
                    break;
                }
            }
        }

        if (isHovering)
        {
            currentSelectionIndex = hoveredIndex;
            UpdateSelectionHighlight(currentSelectionIndex);

            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log($"[Selection] Selected Suspect {currentSelectionIndex}: {spawnedLowDetailSuspects[currentSelectionIndex].name}");
                // Trigger Interrogation Switch in GameManager
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.StartInterrogationFromSelection(currentSelectionIndex);
                }
            }
        }
        else
        {
            // Optional: Clear highlight if not hovering any suspect
            UpdateSelectionHighlight(-1);
        }
    }

    private void UpdateSelectionHighlight(int index)
    {
        // Handle SuspectHighlight components if they exist
        for (int i = 0; i < spawnedLowDetailSuspects.Count; i++)
        {
            var highlight = spawnedLowDetailSuspects[i].GetComponent<SuspectHighlight>();
            if (highlight == null) highlight = spawnedLowDetailSuspects[i].GetComponentInChildren<SuspectHighlight>();
            
            if (highlight != null)
            {
                highlight.SetSelected(i == index);
            }
        }

        // Move Selection Light
        if (selectionLight != null)
        {
            if (index >= 0 && index < spawnedLowDetailSuspects.Count)
            {
                selectionLight.transform.position = spawnedLowDetailSuspects[index].transform.position + lightOffset;
                selectionLight.gameObject.SetActive(true);
            }
            else
            {
                selectionLight.gameObject.SetActive(false);
            }
        }
    }

    public void SetSelectionIndex(int index)
    {
        if (index >= 0 && index < spawnedLowDetailSuspects.Count)
        {
            currentSelectionIndex = index;
            UpdateSelectionHighlight(index);
        }
    }

    public int GetSelectedSuspectIndex()
    {
        return currentSelectionIndex; 
    }
    
    public SuspectData GetSelectedSuspectData()
    {
        if (currentScenarioData != null && currentSelectionIndex >= 0 && currentSelectionIndex < currentScenarioData.suspects.Count)
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
        if (selectionLight != null) selectionLight.gameObject.SetActive(visible);
    }
}
