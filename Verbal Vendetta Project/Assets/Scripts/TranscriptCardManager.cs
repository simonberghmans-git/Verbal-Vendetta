using System.Collections.Generic;
using UnityEngine;

public class TranscriptCardManager : MonoBehaviour
{
    [Header("Dependencies")]
    public ConversationPipeline conversationPipeline;
    
    [Header("Prefabs")]
    public GameObject transcriptCardPrefab;

    [Header("Positions")]
    public Transform hoverLocation;
    public List<Transform> cardSlots; // Max 5 slots ordered from newest (index 0) to oldest (index 4)

    [Header("Testing")]
    public bool spawnTestCardsOnStart = false;

    private string lastPlayerQuestion = "";
    private List<TranscriptCardInteractable> activeCards = new List<TranscriptCardInteractable>();

    private void Start()
    {
        if (spawnTestCardsOnStart)
        {
            // Spawn backwards so the first one ends up at index 4
            for (int i = 5; i >= 1; i--)
            {
                CreateNewCard($"This is test question number {i}?", $"This is test answer {i}. I am saying a sentence to test the wrapping.");
            }
        }
    }

    private void OnEnable()
    {
        if (conversationPipeline != null)
        {
            conversationPipeline.OnTranscriptionReceived += HandleTranscription;
        }
    }

    private void OnDisable()
    {
        if (conversationPipeline != null)
        {
            conversationPipeline.OnTranscriptionReceived -= HandleTranscription;
        }
    }

    private void HandleTranscription(string speaker, string text)
    {
        if (speaker == "Player")
        {
            lastPlayerQuestion = text;
        }
        else if (speaker != "Police Chief") // Assuming we don't want to log Police Chief briefing
        {
            // Suspect answered. Create a paired transcript card
            CreateNewCard(lastPlayerQuestion, text);
            // Reset player question so it isn't reused accidentally
            lastPlayerQuestion = "";
        }
    }

    private void CreateNewCard(string question, string answer)
    {
        if (transcriptCardPrefab == null || cardSlots == null || cardSlots.Count == 0) return;

        string formattedText = $"<b>Q:</b> {question}\n\n<b>A:</b> {answer}";

        // Shift existing cards down one slot
        for (int i = activeCards.Count - 1; i >= 0; i--)
        {
            TranscriptCardInteractable card = activeCards[i];
            int newSlotIndex = i + 1;

            if (newSlotIndex >= cardSlots.Count)
            {
                // Push off the desk / destroy
                activeCards.RemoveAt(i);
                if (card != null) Destroy(card.gameObject);
            }
            else
            {
                // Tell card to lerp to its new slot
                card.UpdateTargetSlot(cardSlots[newSlotIndex]);
            }
        }

        // Spawn new card at slot 0
        GameObject newCardObj = Instantiate(transcriptCardPrefab, cardSlots[0].position, cardSlots[0].rotation);
        TranscriptCardInteractable interactable = newCardObj.GetComponent<TranscriptCardInteractable>();
        
        if (interactable != null)
        {
            interactable.Setup(formattedText, cardSlots[0], hoverLocation);
            activeCards.Insert(0, interactable);
        }
    }

    public void RemoveCard(TranscriptCardInteractable card)
    {
        if (activeCards.Contains(card))
        {
            activeCards.Remove(card);
            // We do not shift the remaining cards up. They stay in their physical slots until a new one pushes them down.
            // This prevents sudden unexpected movement on the desk.
        }
    }

    public void ClearAllCards()
    {
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        activeCards.Clear();
        lastPlayerQuestion = "";
    }
}
