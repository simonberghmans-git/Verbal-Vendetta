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
        
        UpdateSelectionHighlight();
        SetVisible(true);
    }

    public void HandleInput()
    {
        if (!isInputActive || spawnedLowDetailSuspects.Count == 0) return;

        // --- Mouse Click Selection ---
        if (Input.GetMouseButtonDown(0))
        {
            // Prevent selection if clicking on UI
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) 
            {
                Debug.Log("[Selection] Clicked on UI, ignoring.");
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log($"[Selection] Hit object: {hit.transform.name}");
                
                // Check if we hit one of our suspects
                for (int i = 0; i < spawnedLowDetailSuspects.Count; i++)
                {
                    if (hit.transform.IsChildOf(spawnedLowDetailSuspects[i].transform))
                    {
                        Debug.Log($"[Selection] Selected Suspect {i}: {spawnedLowDetailSuspects[i].name}");
                        currentSelectionIndex = i;
                        UpdateSelectionHighlight();
                        break;
                    }
                }
            }
            else
            {
                Debug.Log("[Selection] Raycast hit nothing. Ensure suspects have Colliders.");
            }
        }
    }

    private void UpdateSelectionHighlight()
    {
        if (spawnedLowDetailSuspects.Count == 0) return;

        // Move Selection Light
        if (selectionLight != null)
        {
            selectionLight.transform.position = spawnedLowDetailSuspects[currentSelectionIndex].transform.position + lightOffset;
            selectionLight.gameObject.SetActive(true);
        }

        // Update Character Highlights
        for (int i = 0; i < spawnedLowDetailSuspects.Count; i++)
        {
            var highlight = spawnedLowDetailSuspects[i].GetComponent<SuspectHighlight>();
            if (highlight == null)
            {
                // Add component if missing
                highlight = spawnedLowDetailSuspects[i].AddComponent<SuspectHighlight>();
            }
            highlight.SetSelected(i == currentSelectionIndex);
        }
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
        if (selectionLight != null) selectionLight.gameObject.SetActive(visible);
    }
}
