using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Simple helper to update the visual components of a suspect card.
/// </summary>
public class SuspectCardUI : MonoBehaviour, IPointerClickHandler
{
    public Image portraitImage;
    public TMP_Text nameText;
    public TMP_Text relationshipText;

    private int suspectIndex;

    public void Setup(int index, string suspectName, string relationship, Sprite portrait)
    {
        this.suspectIndex = index;
        if (nameText != null) nameText.text = suspectName;
        if (relationshipText != null) relationshipText.text = relationship;
        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            Debug.Log($"[SuspectCardUI] Double-clicked suspect {suspectIndex}. Entering interrogation.");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartInterrogationFromPinBoard(suspectIndex);
            }
        }
    }
}
