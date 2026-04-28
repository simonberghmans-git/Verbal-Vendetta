using UnityEngine;
using System.Collections.Generic;
using Unity.InferenceEngine;

[System.Serializable]
public struct KokoroVoice
{
    public string voiceName; // e.g., "af_heart", "am_adam"
}


public class SuspectManager : MonoBehaviour
{
    [Header("High Detail Models (For Interrogation)")]
    public List<GameObject> allSuspectPrefabs;

    [Header("Low Detail Models (For Selection Lineup)")]
    public List<GameObject> lowDetailSuspectPrefabs;

    [Header("Kokoro Voice Settings")]
    [Tooltip("List of Kokoro Voices for Male suspects.")]
    public List<string> maleKokoroVoices;
    [Tooltip("List of Kokoro Voices for Female suspects.")]
    public List<string> femaleKokoroVoices;
    [Tooltip("Kokoro Voice for the Police Chief in the Accusation Phase.")]
    public string policeChiefVoice = "am_adam";
    [Tooltip("Kokoro Voice for the Newsreader in the Ending.")]
    public string newsreaderVoice = "af_nicole";

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

        Transform targetSpot = interrogationSpot;
        if (targetSpot == null) 
        {
            Debug.LogError("SuspectManager: No spawn location provided!");
            return null;
        }

        Vector3 finalPosition = targetSpot.position;
        
        // Combine target rotation with offset
        Quaternion finalRotation = targetSpot.rotation * Quaternion.Euler(0, -90, 0);

        return Instantiate(prefab, finalPosition, finalRotation);
    }

    public GameObject SpawnLineupSuspect(int modelId, Transform spawnLocation)
    {
        GameObject prefab = GetLowDetailPrefab(modelId);
        if (prefab == null) return null;

        // Match spawn location rotation
        Quaternion finalRotation = spawnLocation.rotation;

        return Instantiate(prefab, spawnLocation.position, finalRotation);
    }
}
