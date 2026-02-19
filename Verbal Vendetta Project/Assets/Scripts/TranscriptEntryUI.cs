using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TranscriptEntryUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button copyButton;

    private NotebookManager notebookManager;
    private string fullText;

    private int suspectIndex;

    /// <summary>
    /// Setup this entry with text, a reference to the manager, and the suspect index.
    /// </summary>
    public void Setup(string text, NotebookManager manager, int index)
    {
        fullText = text;
        notebookManager = manager;
        suspectIndex = index;

        if (dialogueText != null)
        {
            dialogueText.text = text;
        }

        if (copyButton != null)
        {
            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(OnCopyClicked);
        }
    }

    public void UpdateText(string newText)
    {
        fullText = newText;
        if (dialogueText != null)
        {
            dialogueText.text = newText;
        }
    }

    private void OnCopyClicked()
    {
        if (notebookManager != null)
        {
            notebookManager.ToggleKeyStatement(suspectIndex, fullText);
        }
    }
}
