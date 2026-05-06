using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple helper to update the visual components of a suspect card.
/// </summary>
public class SuspectCardUI : MonoBehaviour
{
    public Image portraitImage;
    public TMP_Text nameText;
    public TMP_Text relationshipText;

    public void Setup(string suspectName, string relationship, Sprite portrait)
    {
        if (nameText != null) nameText.text = suspectName;
        if (relationshipText != null) relationshipText.text = relationship;
        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = true;
        }
    }
}
