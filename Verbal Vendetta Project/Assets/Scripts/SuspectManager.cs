using UnityEngine;
using System.Collections.Generic;

public class SuspectManager : MonoBehaviour
{
    [Header("High Detail Models (For Interrogation)")]
    public List<GameObject> allSuspectPrefabs;

    [Header("Low Detail Models (For Selection Lineup)")]
    public List<GameObject> lowDetailSuspectPrefabs;

    [Header("Voice Settings")]
    [Tooltip("List of Gemini Voice Names for Male suspects.")]
    public List<string> maleVoiceIds;
    [Tooltip("List of Gemini Voice Names for Female suspects.")]
    public List<string> femaleVoiceIds;

    [Header("Model Indices")]
    [Tooltip("Indices in the Prefab List that correspond to Male characters.")]
    public List<int> maleModelIndices;
    [Tooltip("Indices in the Prefab List that correspond to Female characters.")]
    public List<int> femaleModelIndices;

    [Header("Suspect Images")]
    public List<Sprite> suspectHeadshots;

    [Header("Spawn Settings")]
    public Transform interrogationSpot;

    public GameObject GetHighDetailPrefab(int id)
    {
        if (id >= 0 && id < allSuspectPrefabs.Count) return allSuspectPrefabs[id];
        return null;
    }

    public GameObject GetLowDetailPrefab(int id)
    {
        if (id >= 0 && id < lowDetailSuspectPrefabs.Count) return lowDetailSuspectPrefabs[id];
        return null; // Fallback? Or return high detail if low missing?
    }

    public Sprite GetSuspectImage(int id)
    {
        if (id >= 0 && id < suspectHeadshots.Count) return suspectHeadshots[id];
        return null;
    }

    public GameObject SpawnSuspect(int modelId, Transform spawnLocation = null)
    {
        GameObject prefab = GetHighDetailPrefab(modelId);
        if (prefab == null) return null;

        Transform targetSpot = spawnLocation != null ? spawnLocation : interrogationSpot;
        if (targetSpot == null) 
        {
            Debug.LogError("SuspectManager: No spawn location provided!");
            return null;
        }

        Vector3 finalPosition = targetSpot.position;
        
        // Combine target rotation with offset and the 180 flip
        Quaternion finalRotation = targetSpot.rotation * Quaternion.Euler(0, 180, 0);

        return Instantiate(prefab, finalPosition, finalRotation);
    }

    public GameObject SpawnLineupSuspect(int modelId, Transform spawnLocation)
    {
        GameObject prefab = GetLowDetailPrefab(modelId);
        if (prefab == null) return null;

        // Apply 180 degree rotation for lineup suspects too
        Quaternion finalRotation = spawnLocation.rotation * Quaternion.Euler(0, 180, 0);

        return Instantiate(prefab, spawnLocation.position, finalRotation);
    }
}
