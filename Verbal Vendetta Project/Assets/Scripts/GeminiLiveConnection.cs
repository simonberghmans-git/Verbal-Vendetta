using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Manages a persistent WebSocket connection to the Gemini Multimodal Live API.
/// Handles recording mic input, streaming it to the API, and receiving audio.
/// </summary>
public class GeminiLiveConnection : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = "";
    private string model = "models/gemini-2.5-flash-native-audio-preview-12-2025";
    private string wsUrl => $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={apiKey}";

    [Header("Audio Output")]
    [HideInInspector] public AudioSource voiceSource;
    private int outputSampleRate = 24000;
    private Queue<float> audioJitterBuffer = new Queue<float>();
    private List<byte> currentTurnAudioBuffer = new List<byte>();

    [Header("Settings")]
    public bool alwaysListening = true;
    public bool isMuted = false;
    private string micName;
    private int inputSampleRate = 16000;
    private AudioClip recordingClip;
    private int lastSamplePosition = 0;
    private bool isRecording = false;

    // WebSocket state
    private ClientWebSocket webSocket;
    private CancellationTokenSource cts;
    private TaskCompletionSource<bool> setupCompletedTcs;

    // Events
    public delegate void TranscriptionReceivedHandler(string speaker, string text);
    public event TranscriptionReceivedHandler OnTranscriptionReceived;

    public delegate void MetadataReceivedHandler(string startEmotion, string endEmotion, float stressLevel);
    public event MetadataReceivedHandler OnMetadataReceived;

    public delegate void BodyAnimationTriggerHandler(string animationName);
    public event BodyAnimationTriggerHandler OnBodyAnimationTriggered;

    public event Action OnForceDirectEyeContact;

    public delegate void SpeakStateChangedHandler(bool isSpeaking);
    public event SpeakStateChangedHandler OnSpeakStateChanged;

    private float lastAudioTime = 0f;
    private bool isModelCurrentlySpeaking = false;
    private int playedSamplesCount = 0; 

    private SuspectData currentSuspect;
    private bool isConnecting = false;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
        }
    }

    public async void ConnectSession(SuspectData suspect, ScenarioData scenario, AudioSource suspectVoiceSource)
    {
        if (isConnecting) return;
        isConnecting = true;

        try
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                var manager = FindObjectOfType<GeminiConnectionManager>();
                if (manager != null) apiKey = manager.apiKey;
            }

            if (string.IsNullOrEmpty(apiKey)) return;

            currentSuspect = suspect;
            audioJitterBuffer.Clear();
            currentTurnAudioBuffer.Clear();

            voiceSource = suspectVoiceSource;
            if (voiceSource != null)
            {
                voiceSource.clip = AudioClip.Create("LiveAudioStream", outputSampleRate, 1, outputSampleRate, true, OnAudioRead);
                voiceSource.loop = true;
                voiceSource.Play();
            }

            await DisconnectSessionAsync("Starting new session");

            webSocket = new ClientWebSocket();
            cts = new CancellationTokenSource();

            Uri uri = new Uri(wsUrl);
            await webSocket.ConnectAsync(uri, cts.Token);
            Debug.Log($"GeminiLiveConnection: Connected.");

            _ = ReceiveLoop();

            setupCompletedTcs = new TaskCompletionSource<bool>();
            await SendSetupMessage(suspect, scenario);

            var setupTimeout = Task.Delay(5000);
            var completedTask = await Task.WhenAny(setupCompletedTcs.Task, setupTimeout);

            await SendChatHistory(suspect);

            if (alwaysListening)
            {
                StartRecording();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"GeminiLiveConnection: Connection Error: {ex.Message}");
        }
        finally
        {
            isConnecting = false;
        }
    }

    public async Task DisconnectSessionAsync(string reason = "Unknown")
    {
        StopRecording();
        if (webSocket != null)
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    cts?.Cancel();
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session Ended", CancellationToken.None);
                }
                catch { }
            }
            webSocket.Dispose();
            webSocket = null;
        }
        cts?.Dispose();
        cts = null;
        audioJitterBuffer.Clear();
        currentTurnAudioBuffer.Clear();
    }

    private void OnDestroy()
    {
        _ = DisconnectSessionAsync("OnDestroy");
    }

    public void StartRecording()
    {
        if (string.IsNullOrEmpty(micName) || webSocket == null || webSocket.State != WebSocketState.Open) return;

        isRecording = true;
        lastSamplePosition = 0;
        recordingClip = Microphone.Start(micName, true, 10, inputSampleRate);
        Debug.Log($"GeminiLiveConnection: Microphone [{micName}] is OPEN and ready for use.");
    }

    public void StopRecording()
    {
        isRecording = false;
        if (Microphone.IsRecording(micName)) Microphone.End(micName);

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            var endTurnMsg = new { clientContent = new { turnComplete = true } };
            _ = SendClientContent(endTurnMsg, cts.Token);
        }
    }

    void Update()
    {
        bool hasAudio = false;
        lock (audioJitterBuffer)
        {
            hasAudio = audioJitterBuffer.Count > 0;
        }

        if (hasAudio)
        {
            lastAudioTime = Time.time;
            if (!isModelCurrentlySpeaking)
            {
                isModelCurrentlySpeaking = true;
                playedSamplesCount = 0; 
                OnSpeakStateChanged?.Invoke(true);
            }
        }
        else if (isModelCurrentlySpeaking && Time.time - lastAudioTime > 0.3f)
        {
            isModelCurrentlySpeaking = false;
            OnSpeakStateChanged?.Invoke(false);
        }

        if (isRecording && Microphone.IsRecording(micName))
        {
            int currentPosition = Microphone.GetPosition(micName);
            if (currentPosition < 0 || lastSamplePosition == currentPosition) return;

            int sampleDiff = currentPosition - lastSamplePosition;
            if (sampleDiff < 0) sampleDiff += recordingClip.samples;

            if (sampleDiff > inputSampleRate * 0.1f) 
            {
                float[] samples = new float[sampleDiff];
                
                if (lastSamplePosition + sampleDiff > recordingClip.samples)
                {
                    int endLength = recordingClip.samples - lastSamplePosition;
                    float[] endSamples = new float[endLength];
                    recordingClip.GetData(endSamples, lastSamplePosition);
                    
                    int wrapLength = sampleDiff - endLength;
                    float[] wrapSamples = new float[wrapLength];
                    recordingClip.GetData(wrapSamples, 0);
                    
                    endSamples.CopyTo(samples, 0);
                    wrapSamples.CopyTo(samples, endLength);
                }
                else
                {
                    recordingClip.GetData(samples, lastSamplePosition);
                }

                lastSamplePosition = currentPosition;
                
                float sum = 0f;
                for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
                float rmsValue = Mathf.Sqrt(sum / samples.Length);
                
                float volumeThreshold = 0.02f;
                bool isPlayerSpeaking = rmsValue > volumeThreshold;

                if (isModelCurrentlySpeaking && isPlayerSpeaking && !isMuted)
                {
                    // Local visual logging only. We rely on the continuous SendAudioChunk 
                    // below to trigger the server's VAD, which handles the real interruption.
                    Debug.Log("GeminiLiveConnection: Player speaking loudly, expecting server interrupt...");
                }
                
                // Keep streaming audio to the server. The server's VAD will automatically 
                // cut off the model when it hears this audio feed and send the interrupted flag.
                if (!isMuted)
                {
                    SendAudioChunk(samples);
                }
            }
        }
    }

    public void HandleServerInterruption()
    {
        Debug.Log("GeminiLiveConnection: Server confirmed interruption. Halting playback.");
        
        lock (audioJitterBuffer)
        {
            audioJitterBuffer.Clear();
        }
        
        // Let the AudioSource keep running the OnAudioRead loop, but feeding it 0s (since buffer is clear)
        // This is smoother than hard-stopping the AudioSource component and avoids Unity audio popping.

        isModelCurrentlySpeaking = false;
        OnSpeakStateChanged?.Invoke(false);
    }

    private async void SendAudioChunk(float[] samples)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open) return;

        byte[] pcmBytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short pcm = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
            byte[] bytes = BitConverter.GetBytes(pcm);
            pcmBytes[i * 2] = bytes[0];
            pcmBytes[i * 2 + 1] = bytes[1];
        }

        string base64Audio = Convert.ToBase64String(pcmBytes);
        var payload = new
        {
            realtimeInput = new
            {
                mediaChunks = new[]
                {
                    new { mimeType = $"audio/pcm;rate={inputSampleRate}", data = base64Audio }
                }
            }
        };
        await SendClientContent(payload, cts.Token);
    }

    private async Task SendSetupMessage(SuspectData suspect, ScenarioData scenario)
    {
        string timelineContext = $"TODAY IS: {scenario.interrogation_date}. THE MURDER HAPPENED ON: {scenario.murder_date} AT {scenario.murder_time}.";
        string systemPrompt = $@"You are roleplaying as {suspect.name}.
        {timelineContext}
        RELATIONSHIP: {suspect.relationship}.
        TRAITS: {suspect.personality}.
        ALIBI: {suspect.alibi_statement}.
        MOTIVE: {(suspect.has_motive ? suspect.motive : "None.")}.
        ACCESS: {(suspect.has_access_to_weapon ? suspect.access_to_weapon_description : "None.")}.
        GUILT: {(suspect.is_killer ? "YOU ARE THE KILLER." : "INNOCENT.")}.
        RUMORS YOU KNOW: {JsonConvert.SerializeObject(suspect.rumors)}.
        
        RULES: 
        1. Stay in character. Respond to the detective's real-time spoken queries.
        2. Keep answers concise (1-3 sentences) suited for natural conversation.
        3. Do not use colon symbols when referring to time, say '3 45 PM'.
        4. CRITICAL: Before you speak, you MUST ALWAYS call the 'SetEmotion' tool to reflect your current emotional state.
        5. You MUST ONLY use one of the exact following emotions: Neutral, Angry, Shocked, Sad, Smug, Nervous, Guilty. Do NOT use any other words or synonyms.
        6. CRITICAL: If the question is very easy to answer without remembering details, you MUST call the 'ForceDirectEyeContact' tool.
        7. When asked about things that do not at all relate to the case, point out the absurdity of the question.
        8. When pressured about their false alibi, only the suspect with no motive and a false alibi (= Red Herring) will reveal their minor secret, explaining why they would fake their alibi.
        9. Refer only to what your character knows as described in the JSON file.
        10. You MUST call the 'TriggerBodyAnimation' tool whenever your response is applicable to one of the animation options.";

        var setupMsg = new
        {
            setup = new
            {
                model = this.model,
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new { prebuiltVoiceConfig = new { voiceName = string.IsNullOrEmpty(suspect.voice_id) ? "Puck" : suspect.voice_id } }
                    }
                },
                systemInstruction = new { parts = new[] { new { text = systemPrompt } }, role = "user" },
                tools = new[]
                {
                    new {
                        functionDeclarations = new[] {
                            new {
                                name = "SetEmotion",
                                description = "Updates your physical facial expression.",
                                parameters = new {
                                    type = "OBJECT",
                                    properties = new Dictionary<string, object> {
                                        { "emotion", new { 
                                            type = "STRING", 
                                            description = "One of: Neutral, Angry, Shocked, Sad, Smug, Nervous, Guilty",
                                            @enum = new[] { "Neutral", "Angry", "Shocked", "Sad", "Smug", "Nervous", "Guilty" }
                                        } }
                                    },
                                    required = new[] { "emotion" }
                                }
                            },
                            new {
                                name = "TriggerBodyAnimation",
                                description = "Triggers a specific body animation to emphasize your response. Use this only when appropriate.",
                                parameters = new {
                                    type = "OBJECT",
                                    properties = new Dictionary<string, object> {
                                        { "animationName", new { 
                                            type = "STRING", 
                                            description = "One of:Dissaproval, Disbelief, Fist",
                                            @enum = new[] { "Dissaproval", "Disbelief", "Fist" }
                                        } }
                                    },
                                    required = new[] { "animationName" }
                                }
                            },
                            new {
                                name = "ForceDirectEyeContact",
                                description = "Triggers direct eye contact with the detective. Use this ONLY when the question is very easy to answer without needing to remember details.",
                                parameters = new {
                                    type = "OBJECT",
                                    properties = new Dictionary<string, object>(),
                                    required = new string[] {}
                                }
                            }
                        }
                    }
                }
            }
        };
        await SendClientContent(setupMsg, cts.Token);
    }

    private async Task SendChatHistory(SuspectData suspect)
    {
        if (suspect.chatHistory == null || suspect.chatHistory.Count == 0) return;

        List<object> turns = new List<object>();
        foreach (var msg in suspect.chatHistory)
        {
            turns.Add(new { role = msg.role, parts = new[] { new { text = msg.parts[0].text } } });
        }

        var historyMsg = new { clientContent = new { turns = turns.ToArray(), turnComplete = true } };
        await SendClientContent(historyMsg, cts.Token);
    }

    private async Task SendClientContent(object payload, CancellationToken token)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open) return;
        string json = JsonConvert.SerializeObject(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);
        await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
    }



    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 64];
        try
        {
            while (webSocket.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                var sb = new StringBuilder();
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectSessionAsync($"Server Closed");
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                ProcessServerMessage(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Debug.LogError($"GeminiLiveConnection: Receive Loop Error: {ex.Message}"); }
    }

    private void ProcessServerMessage(string jsonResponse)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<LiveServerMessage>(jsonResponse);

            if (msg.server_content != null && msg.server_content.interrupted)
            {
                HandleServerInterruption();

                byte[] audioToTranscribe = null;
                lock (currentTurnAudioBuffer)
                {
                    if (currentTurnAudioBuffer.Count > 0)
                    {
                        int playedBytesCount = playedSamplesCount * 2;
                        if (playedBytesCount > currentTurnAudioBuffer.Count) playedBytesCount = currentTurnAudioBuffer.Count;
                             
                        audioToTranscribe = currentTurnAudioBuffer.GetRange(0, playedBytesCount).ToArray();
                        currentTurnAudioBuffer.Clear();
                    }
                }
                     
                if (audioToTranscribe != null && audioToTranscribe.Length > 0)
                {
                    StartCoroutine(TranscribeAudioRoutine(currentSuspect?.name ?? "Model", audioToTranscribe));
                }
                
                // Keep returning to discard any model turn chunks in this exact interrupted payload
                return;
            }

            if (msg.server_content != null && msg.server_content.model_turn != null)
            {
                foreach (var part in msg.server_content.model_turn.parts)
                {
                    if (part.inline_data != null && !string.IsNullOrEmpty(part.inline_data.data))
                    {
                        byte[] pcmBytes = Convert.FromBase64String(part.inline_data.data);
                        QueueAudio(pcmBytes);
                        
                        lock (currentTurnAudioBuffer)
                        {
                            currentTurnAudioBuffer.AddRange(pcmBytes);
                        }
                    }
                }
            }
            else if (msg.setup_complete != null)
            {
                setupCompletedTcs?.TrySetResult(true);
            }
            
            if (msg.tool_call != null && msg.tool_call.functionCalls != null)
            {
                var functionResponses = new List<object>();

                foreach (var call in msg.tool_call.functionCalls)
                {
                    if (call.name == "SetEmotion" && call.args != null && call.args.ContainsKey("emotion"))
                    {
                        string emotionStr = call.args["emotion"].ToString();
                        OnMetadataReceived?.Invoke(emotionStr, emotionStr, 0.5f);
                        functionResponses.Add(new { id = call.id, name = call.name, response = new { result = "success" } });
                    }
                    else if (call.name == "TriggerBodyAnimation" && call.args != null && call.args.ContainsKey("animationName"))
                    {
                        string animName = call.args["animationName"].ToString();
                        OnBodyAnimationTriggered?.Invoke(animName);
                        functionResponses.Add(new { id = call.id, name = call.name, response = new { result = "success" } });
                    }
                    else if (call.name == "ForceDirectEyeContact")
                    {
                        OnForceDirectEyeContact?.Invoke();
                        functionResponses.Add(new { id = call.id, name = call.name, response = new { result = "success" } });
                    }
                }

                if (functionResponses.Count > 0)
                {
                    var payload = new { toolResponse = new { functionResponses = functionResponses.ToArray() } };
                    _ = SendClientContent(payload, cts.Token);
                }
            }
            
            if (msg.server_content != null && msg.server_content.turnComplete)
            {
                byte[] audioToTranscribe = null;
                lock (currentTurnAudioBuffer)
                {
                    if (currentTurnAudioBuffer.Count > 0)
                    {
                        audioToTranscribe = currentTurnAudioBuffer.ToArray();
                        currentTurnAudioBuffer.Clear();
                    }
                }
                     
                if (audioToTranscribe != null && audioToTranscribe.Length > 0)
                {
                    StartCoroutine(TranscribeAudioRoutine(currentSuspect?.name ?? "Model", audioToTranscribe));
                }
            }
        }
        catch (Exception) { }
    }

    private IEnumerator TranscribeAudioRoutine(string speakerName, byte[] pcm16Data)
    {
        byte[] wavBytes = AppendWavHeader(pcm16Data, 24000);
        string base64Audio = Convert.ToBase64String(wavBytes);

        string sttModel = "gemini-2.0-flash"; 
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{sttModel}:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = "Transcribe the EXACT spoken dialogue in this audio clip. Reply with ONLY the transcript, nothing else. Do not use quotes." },
                        new { inline_data = new { mime_type = "audio/wav", data = base64Audio } }
                    }
                }
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);
        
        int maxRetries = 2;
        int currentTry = 0;
        bool success = false;

        while (currentTry < maxRetries && !success)
        {
            currentTry++;
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.uploadHandler.contentType = "application/json";
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    success = true;
                    try
                    {
                        var res = JsonConvert.DeserializeObject<GeminiResponseWrapper>(request.downloadHandler.text);
                        string transcript = res.candidates[0].content.parts[0].text.Trim();

                        OnTranscriptionReceived?.Invoke(speakerName, transcript);
                        AppendToChatHistory("model", transcript);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("GeminiLiveConnection STT Parse Error: " + e.Message);
                    }
                }
                else
                {
                    Debug.LogWarning($"GeminiLiveConnection STT API Error (Try {currentTry}): {request.error}");
                    if (currentTry < maxRetries) yield return new WaitForSeconds(1.5f);
                }
            }
        }
    }
    
    public void AppendPlayerTextToHistory(string playerText)
    {
        OnTranscriptionReceived?.Invoke("Player", playerText);
        AppendToChatHistory("user", playerText);
    }

    private void AppendToChatHistory(string role, string text)
    {
        if (currentSuspect == null) return;
        if (currentSuspect.chatHistory.Count > 0 && currentSuspect.chatHistory[^1].role == role)
        {
             currentSuspect.chatHistory[^1].parts[0].text += " " + text;
        }
        else
        {
             currentSuspect.chatHistory.Add(new GeminiConnectionManager.GeminiContent() { role = role, parts = new List<GeminiConnectionManager.GeminiPart> { new GeminiConnectionManager.GeminiPart() { text = text } }});
        }
    }

    private void QueueAudio(byte[] pcmBytes)
    {
        int sampleCount = pcmBytes.Length / 2;
        lock (audioJitterBuffer)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(pcmBytes, i * 2);
                audioJitterBuffer.Enqueue(sample / 32768f);
            }
        }
    }

    private void OnAudioRead(float[] data)
    {
        lock (audioJitterBuffer)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (audioJitterBuffer.Count > 0) 
                {
                    data[i] = audioJitterBuffer.Dequeue();
                    if (isModelCurrentlySpeaking)
                    {
                        playedSamplesCount++; 
                    }
                }
                else data[i] = 0f;
            }
        }
    }

    private byte[] AppendWavHeader(byte[] pcmData, int sampleRate)
    {
        byte[] header = new byte[44];
        int byteRate = sampleRate * 2;
        int dataLen = pcmData.Length;
        int riffLen = dataLen + 36;
        
        header[0] = (byte)'R'; header[1] = (byte)'I'; header[2] = (byte)'F'; header[3] = (byte)'F';
        header[4] = (byte)(riffLen & 0xff); header[5] = (byte)((riffLen >> 8) & 0xff); header[6] = (byte)((riffLen >> 16) & 0xff); header[7] = (byte)((riffLen >> 24) & 0xff);
        header[8] = (byte)'W'; header[9] = (byte)'A'; header[10] = (byte)'V'; header[11] = (byte)'E';
        header[12] = (byte)'f'; header[13] = (byte)'m'; header[14] = (byte)'t'; header[15] = (byte)' ';
        header[16] = 16; header[17] = 0; header[18] = 0; header[19] = 0;
        header[20] = 1; header[21] = 0; 
        header[22] = 1; header[23] = 0; 
        header[24] = (byte)(sampleRate & 0xff); header[25] = (byte)((sampleRate >> 8) & 0xff); header[26] = (byte)((sampleRate >> 16) & 0xff); header[27] = (byte)((sampleRate >> 24) & 0xff);
        header[28] = (byte)(byteRate & 0xff); header[29] = (byte)((byteRate >> 8) & 0xff); header[30] = (byte)((byteRate >> 16) & 0xff); header[31] = (byte)((byteRate >> 24) & 0xff);
        header[32] = 2; header[33] = 0; 
        header[34] = 16; header[35] = 0; 
        header[36] = (byte)'d'; header[37] = (byte)'a'; header[38] = (byte)'t'; header[39] = (byte)'a';
        header[40] = (byte)(dataLen & 0xff); header[41] = (byte)((dataLen >> 8) & 0xff); header[42] = (byte)((dataLen >> 16) & 0xff); header[43] = (byte)((dataLen >> 24) & 0xff);

        byte[] wavBlock = new byte[header.Length + pcmData.Length];
        Buffer.BlockCopy(header, 0, wavBlock, 0, header.Length);
        Buffer.BlockCopy(pcmData, 0, wavBlock, header.Length, pcmData.Length);
        return wavBlock;
    }

    [Serializable] private class LiveServerMessage {
        [JsonProperty("serverContent")] public ServerContent server_content;
        [JsonProperty("setupComplete")] public ReceiveSetupResponse setup_complete;
        [JsonProperty("toolCall")] public ToolCallMessage tool_call; 
    }
    [Serializable] private class ServerContent {
        [JsonProperty("modelTurn")] public ModelTurn model_turn;
        [JsonProperty("interrupted")] public bool interrupted;
        [JsonProperty("turnComplete")] public bool turnComplete;
    }
    [Serializable] private class ModelTurn { public List<Part> parts; }
    [Serializable] private class Part {
        [JsonProperty("text")] public string text;
        [JsonProperty("thought")] public bool thought;
        [JsonProperty("inlineData")] public InlineData inline_data;
    }
    [Serializable] private class InlineData {
        [JsonProperty("mimeType")] public string mime_type;
        [JsonProperty("data")] public string data;
    }
    [Serializable] private class ReceiveSetupResponse { }
    
    [Serializable] private class ToolCallMessage {
        [JsonProperty("functionCalls")] public List<FunctionCall> functionCalls;
    }
    [Serializable] private class FunctionCall {
        [JsonProperty("id")] public string id;
        [JsonProperty("name")] public string name;
        [JsonProperty("args")] public Dictionary<string, object> args;
    }

    [Serializable] private class GeminiResponseWrapper { public List<GeminiCandidate> candidates; }
    [Serializable] private class GeminiCandidate { public GeminiContent content; }
    [Serializable] private class GeminiContent { public List<GeminiPart> parts; }
    [Serializable] private class GeminiPart { public string text; }
}