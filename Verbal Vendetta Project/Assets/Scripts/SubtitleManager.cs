using UnityEngine;
using TMPro;
using System.Collections;

public class SubtitleManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text subtitleText;

    [Header("Settings")]
    public float displayDuration = 7f;

    private ConversationPipeline pipeline;
    private Coroutine hideCoroutine;

    private void Start()
    {
        pipeline = FindObjectOfType<ConversationPipeline>();
        if (pipeline != null)
        {
            pipeline.OnTranscriptionReceived += HandleTranscriptionReceived;
        }
        else
        {
            Debug.LogWarning("SubtitleManager: Could not find ConversationPipeline in the scene.");
        }

        if (subtitleText != null)
        {
            subtitleText.text = "";
            subtitleText.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (pipeline != null)
        {
            pipeline.OnTranscriptionReceived -= HandleTranscriptionReceived;
        }
    }

    public void Show(string speaker, string text, float duration)
    {
        if (subtitleText == null) return;

        subtitleText.text = $"<b>{speaker}:</b> {text}";
        subtitleText.gameObject.SetActive(true);

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(HideAfter(duration));
    }

    public void Hide()
    {
        if (subtitleText == null) return;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        subtitleText.gameObject.SetActive(false);
        subtitleText.text = "";
    }

    private void HandleTranscriptionReceived(string speaker, string text)
    {
        // Do not display if not in interrogation
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.Interrogation)
            return;

        Show(speaker, text, displayDuration);
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        Hide();
    }
}
