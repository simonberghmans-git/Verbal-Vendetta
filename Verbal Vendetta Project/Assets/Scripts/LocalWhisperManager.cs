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
        if (microphoneRecord == null || whisperManager == null) return;
        if (!whisperManager.IsLoaded) return;

        microphoneRecord.StartRecord();
    }

    public void StopRecording()
    {
        if (microphoneRecord != null && microphoneRecord.IsRecording)
        {
            microphoneRecord.StopRecord();
        }
    }

    private async void OnRecordStop(AudioChunk chunk)
    {
        if (chunk.Data == null || chunk.Data.Length == 0) return;

        float lengthInSeconds = (float)chunk.Data.Length / chunk.Frequency / chunk.Channels;
        if (lengthInSeconds < 0.2f) return;

        try
        {
            var res = await whisperManager.GetTextAsync(chunk.Data, chunk.Frequency, chunk.Channels);
            if (res != null && !string.IsNullOrWhiteSpace(res.Result))
            {
                string text = res.Result.Trim();
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
