using UnityEngine;
using System;
using System.Collections;
using Whisper;
using Whisper.Utils;

public class LocalWhisperManager : MonoBehaviour
{
    [Header("Whisper Settings")]
    public WhisperManager whisperManager;
    public MicrophoneRecord microphoneRecord;
    
    public event Action<string> OnTranscriptionReceived;
    public event Action<string> OnError;

    private void Awake()
    {
        if (whisperManager == null) whisperManager = GetComponent<WhisperManager>();
        if (microphoneRecord == null) microphoneRecord = GetComponent<MicrophoneRecord>();
        
        if (microphoneRecord != null)
        {
            microphoneRecord.OnRecordStop += OnRecordStop;
        }
    }

    public void StartRecording()
    {
        if (microphoneRecord == null || whisperManager == null)
        {
            Debug.LogError("WhisperManager or MicrophoneRecord is not set!");
            OnError?.Invoke("Microphone or STT missing.");
            return;
        }

        if (!whisperManager.IsLoaded)
        {
            Debug.LogError("Whisper model is not loaded!");
            OnError?.Invoke("STT model not loaded.");
            return;
        }

        microphoneRecord.StartRecord();
        Debug.Log("Local Whisper started recording...");
    }

    public void StopRecording()
    {
        if (microphoneRecord != null && microphoneRecord.IsRecording)
        {
            microphoneRecord.StopRecord();
            Debug.Log("Local Whisper stopped recording. Processing...");
        }
    }

    private async void OnRecordStop(AudioChunk chunk)
    {
        if (chunk.Data == null || chunk.Data.Length == 0)
        {
            OnError?.Invoke("No audio data recorded.");
            return;
        }

        try
        {
            var res = await whisperManager.GetTextAsync(chunk.Data, chunk.Frequency, chunk.Channels);
            if (res != null && !string.IsNullOrWhiteSpace(res.Result))
            {
                string text = res.Result.Trim();
                Debug.Log($"Local Whisper transcribed: {text}");
                OnTranscriptionReceived?.Invoke(text);
            }
            else
            {
                OnTranscriptionReceived?.Invoke(""); // Empty transcript
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Whisper transcription failed: {ex.Message}");
            OnError?.Invoke(ex.Message);
        }
    }
}
