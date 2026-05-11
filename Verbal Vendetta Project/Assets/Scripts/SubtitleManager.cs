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

    private void Update()
    {
        // Only visible during interrogation
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.Interrogation)
        {
            if (subtitleText != null && subtitleText.gameObject.activeSelf)
            {
                subtitleText.gameObject.SetActive(false);
                if (hideCoroutine != null)
                {
                    StopCoroutine(hideCoroutine);
                    hideCoroutine = null;
                }
            }
        }
    }

    private void HandleTranscriptionReceived(string speaker, string text)
    {
        // Do not display if not in interrogation
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.Interrogation)
            return;

        if (subtitleText == null) return;

        // Display the speaker and text
        subtitleText.text = $"<b>{speaker}:</b> {text}";
        subtitleText.gameObject.SetActive(true);

        // Prevent overlap by stopping any existing hide coroutine
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        // Start a new hide coroutine
        hideCoroutine = StartCoroutine(HideSubtitleAfterDelay());
    }

    private IEnumerator HideSubtitleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(false);
            subtitleText.text = "";
        }
    }
}
