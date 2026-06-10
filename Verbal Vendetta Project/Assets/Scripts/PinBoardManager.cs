using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages the pin board evidence system.
/// </summary>
public class PinBoardManager : MonoBehaviour
{
    public static PinBoardManager Instance;

    [Header("UI Roots")]
    public GameObject boardRoot;
    public Transform cardContainer;     // Where cards and scraps are spawned

    [Header("Prefabs")]
    public GameObject suspectCardPrefab;
    public GameObject evidenceScrapPrefab;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.Tab;
    public bool IsOpen => boardRoot != null && boardRoot.activeSelf;
    public List<Transform> suspectSpawnPoints;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (boardRoot != null) boardRoot.SetActive(false);
    }

    private void Update()
    {
        // Toggle handled by GameManager to manage state transitions correctly
    }

    public void SetVisible(bool visible)
    {
        if (boardRoot != null) boardRoot.SetActive(visible);
    }

    /// <summary>
    /// Spawns the initial suspect Polaroids based on the scenario.
    /// </summary>
    public void PopulateBoard(ScenarioData scenario)
    {
        if (scenario == null || suspectCardPrefab == null || cardContainer == null) return;

        // Clear existing items (Disabled to keep manually placed items)
        // foreach (Transform child in cardContainer)
        // {
        //     Destroy(child.gameObject);
        // }

        SuspectManager suspectManager = FindAnyObjectByType<SuspectManager>();

        for (int i = 0; i < scenario.suspects.Count; i++)
        {
            var data = scenario.suspects[i];
            GameObject card = Instantiate(suspectCardPrefab, cardContainer);
            
            if (suspectSpawnPoints != null && i < suspectSpawnPoints.Count && suspectSpawnPoints[i] != null)
            {
                card.transform.position = suspectSpawnPoints[i].position;
                card.transform.rotation = suspectSpawnPoints[i].rotation;
            }
            else
            {
                RectTransform rt = card.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(Random.Range(-400, 400), Random.Range(-200, 200));
            }

            var ui = card.GetComponent<SuspectCardUI>();
            if (ui != null)
            {
                Sprite mugshot = suspectManager != null ? suspectManager.GetSuspectImage(data.model_id) : null;
                ui.Setup(i, data.name, data.relationship, mugshot);
            }
        }
    }

    /// <summary>
    /// Adds a new draggable scrap of paper with the given text.
    /// </summary>
    public void AddEvidenceScrap(string text)
    {
        if (string.IsNullOrEmpty(text) || evidenceScrapPrefab == null || cardContainer == null) return;

        GameObject scrap = Instantiate(evidenceScrapPrefab, cardContainer);
        
        RectTransform rt = scrap.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(Random.Range(-100, 100), Random.Range(-100, 100));

        var txt = scrap.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.text = text;
        }

        Debug.Log($"[PinBoard] Added evidence scrap: {text}");
    }
}
