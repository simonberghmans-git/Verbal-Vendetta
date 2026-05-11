using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { SubjectSelection, Interrogation, Ending, Accusation, PinBoard }
    public GameState currentState = GameState.SubjectSelection;

    [Header("Dependencies")]
    public InterrogationManager interrogationManager;
    public SelectionManager selectionManager;
    public SuspectManager suspectManager;
    public GeminiConnectionManager connectionManager;
    public InterrogationInputManager inputManager; // Added reference
    public Camera mainCamera;

    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public TMP_Text loadingText;

    public void ShowLoadingScreen(string message)
    {
        if (loadingScreen != null) loadingScreen.SetActive(true);
        if (loadingText != null) loadingText.text = message;
    }

    public void HideLoadingScreen()
    {
        if (loadingScreen != null) loadingScreen.SetActive(false);
        if (loadingText != null) loadingText.text = "";
    }

    [Header("Interrogation Scene")]
    public Transform interrogationSpot;
    public Transform interrogationCameraPos;
    public Transform pinBoardCameraPos;

    // Internal State
    private GameObject currentActiveHighDetailModel;
    public bool isInputLocked = false;
    private GameState stateBeforePinBoard = GameState.SubjectSelection;
    private AudioClip briefingClip;
    private AudioSource briefingAudioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (inputManager == null) inputManager = FindObjectOfType<InterrogationInputManager>();

        briefingAudioSource = GetComponent<AudioSource>();
        if (briefingAudioSource == null) briefingAudioSource = gameObject.AddComponent<AudioSource>();

        // Initial Setup - Camera to Selection
        if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
        {
            mainCamera.transform.position = selectionManager.cameraPosition.position;
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation * Quaternion.Euler(0, 180, 0);
        }

        // Ensure cursor is free for selection
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Wait for Generation, then Spawn Lineup
        if (connectionManager != null)
        {
             ShowLoadingScreen("Generating Scenario...");
             connectionManager.GenerateScenario(async (data, error) =>
             {
                 if (data != null)
                 {
                     // Delegate Spawning to SelectionManager
                     if (selectionManager != null)
                     {
                         selectionManager.SpawnLineup(data);
                         selectionManager.isInputActive = true;
                     }

                     if (interrogationManager != null)
                     {
                         interrogationManager.SetActiveSuspect(null, null);
                     }

                     // Generate Briefing Audio for Pin Board
                     if (interrogationManager != null && interrogationManager.conversationPipeline != null)
                     {
                         // Populate the board with suspect cards
                         if (PinBoardManager.Instance != null) PinBoardManager.Instance.PopulateBoard(data);

                         ShowLoadingScreen("Synthesizing Briefing...");
                         await GenerateBriefing(data);
                     }

                     HideLoadingScreen();
                 }
                 else
                 {
                     HideLoadingScreen();
                     Debug.LogError("Generation Failed: " + error);
                 }
             });
        }
    }

    private async Task GenerateBriefing(ScenarioData data)
    {
        Debug.Log("[GameManager] Starting Briefing Synthesis...");
        briefingClip = await interrogationManager.conversationPipeline.GenerateBriefingAudio(data);
        
        if (briefingClip != null)
        {
            Debug.Log($"[GameManager] Briefing synthesized successfully. Length: {briefingClip.length}s");
            // Auto-play the briefing
            ReplayBriefing();
        }
        else
        {
            Debug.LogWarning("[GameManager] Briefing synthesis failed.");
        }
    }

    public void ReplayBriefing()
    {
        if (briefingClip != null && briefingAudioSource != null)
        {
            Debug.Log("[GameManager] Playing Briefing...");
            briefingAudioSource.clip = briefingClip;
            briefingAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("[GameManager] Cannot replay briefing: Clip or AudioSource missing.");
        }
    }

    public void StopBriefing()
    {
        if (briefingAudioSource != null && briefingAudioSource.isPlaying)
        {
            Debug.Log("[GameManager] Stopping Briefing playback.");
            briefingAudioSource.Stop();
        }
    }

    void Update()
    {
        // Microphone Mute Toggle
        if (Input.GetKeyDown(KeyCode.M) && currentState == GameState.Interrogation)
        {
            if (inputManager != null) inputManager.ToggleMute();
        }

        // Briefing Replay/Stop Shortcuts
        if (Input.GetKeyDown(KeyCode.B))
        {
            ReplayBriefing();
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            StopBriefing();
        }

        if (isInputLocked) return;

        if (currentState == GameState.SubjectSelection)
        {
            if (selectionManager != null) selectionManager.HandleInput();
            
            // Check for Toggle
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Check if Pin Board is open - if so, disable mode switching
                if (PinBoardManager.Instance != null && PinBoardManager.Instance.IsOpen)
                {
                    return; 
                }

                StartCoroutine(SwitchToInterrogation());
            }

            // Continuous UI Update removed because suspectNameDisplay was deleted

            // ENTER to start Accusation Phase
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartAccusationPhase();
            }
        }
        else if (currentState == GameState.Interrogation)
        {
            // Press Space to go back
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Check if Pin Board is open - if so, disable mode switching
                if (PinBoardManager.Instance != null && PinBoardManager.Instance.IsOpen)
                {
                    return; 
                }

                StartCoroutine(SwitchToSelection());
            }
        }

        // --- GLOBAL PIN BOARD TOGGLE (TAB) ---
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // Do not allow toggling the Pin Board during the Accusation phase
            if (currentState == GameState.Accusation)
            {
                return;
            }

            if (currentState == GameState.PinBoard)
            {
                StartCoroutine(ClosePinBoard());
            }
            else if (currentState == GameState.SubjectSelection || currentState == GameState.Interrogation)
            {
                StartCoroutine(OpenPinBoard());
            }
        }
    }

    private System.Collections.IEnumerator SwitchToInterrogation()
    {
        if (selectionManager == null) yield break;
        if (inputManager != null && inputManager.micImage != null) inputManager.micImage.gameObject.SetActive(true);
        isInputLocked = true;
        currentState = GameState.Interrogation;

        // Stop the briefing if it's playing
        StopBriefing();

        // Cursor should be locked during interrogation
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 1. Get Data from Selection Manager
        SuspectData activeData = selectionManager.GetSelectedSuspectData();
        int modelId = activeData.model_id;

        // 2. Hide Lineup
        selectionManager.isInputActive = false;
        selectionManager.SetVisible(false);

        // 3. Move Camera
        if (mainCamera != null && interrogationCameraPos != null)
        {
            mainCamera.transform.position = interrogationCameraPos.position;
            mainCamera.transform.rotation = interrogationCameraPos.rotation * Quaternion.Euler(0, 90, 0);
        }

        // 4. Spawn High Detail Model at Interrogation Spot
        if (currentActiveHighDetailModel != null) Destroy(currentActiveHighDetailModel);

        if (suspectManager != null)
        {
            // Use SuspectManager to spawn with offsets
            currentActiveHighDetailModel = suspectManager.SpawnSuspect(modelId, interrogationSpot);
            
            if (currentActiveHighDetailModel != null)
            {
                // 5. Inform InterrogationManager
                interrogationManager.SetActiveSuspect(activeData, currentActiveHighDetailModel);
                
                // Randomize floor point for new interrogation
                if (EyePointManager.Instance != null)
                {
                    EyePointManager.Instance.RandomizeFloorPoint();
                }
            }
        }

        yield return new WaitForSeconds(0.5f); 
        isInputLocked = false;
    }

    private System.Collections.IEnumerator SwitchToSelection()
    {
        // 0. Cancel any pending input
        if (inputManager != null)
        {
            inputManager.ForceReset();
        }
        if (inputManager != null && inputManager.micImage != null) inputManager.micImage.gameObject.SetActive(false);
        isInputLocked = true;
        currentState = GameState.SubjectSelection;

        // Unlock cursor for selection
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 1. Destroy High Detail Model
        if (currentActiveHighDetailModel != null)
        {
            Destroy(currentActiveHighDetailModel);
            currentActiveHighDetailModel = null;
        }

        // 2. Move Camera Back
        if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
        {
            mainCamera.transform.position = selectionManager.cameraPosition.position;
            mainCamera.transform.rotation = selectionManager.cameraPosition.rotation * Quaternion.Euler(0, 180, 0);
        }

        // 3. Show Lineup
        if (selectionManager != null)
        {
            selectionManager.SetVisible(true);
            selectionManager.isInputActive = true;
        }
        
        // Reset Interrogation UI text?
        if (interrogationManager != null)
        {
            interrogationManager.StopInterrogation(); // Cancel All Processes
            interrogationManager.SetActiveSuspect(null, null); // Clear active suspect
        }

        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }

    public void StartAccusationPhase()
    {
        if (isInputLocked) return;
        StartCoroutine(SwitchToAccusation());
    }

    private System.Collections.IEnumerator SwitchToAccusation()
    {
        if (inputManager != null) inputManager.ForceReset();
        
        isInputLocked = true;
        currentState = GameState.Accusation;
        if (inputManager != null && inputManager.micImage != null) inputManager.micImage.gameObject.SetActive(true);
        
        if (selectionManager != null)
        {
            selectionManager.isInputActive = false;
            // selectionManager.SetVisible(false); // KEEP VISIBLE
        }

        if (currentActiveHighDetailModel != null)
        {
            Destroy(currentActiveHighDetailModel);
            currentActiveHighDetailModel = null;
        }
        
        if (interrogationManager != null)
        {
            interrogationManager.StopInterrogation();
            interrogationManager.PrepareAccusationUI();
            
            if (interrogationManager.conversationPipeline != null)
            {
                interrogationManager.conversationPipeline.ConnectSession(null, true);
            }
        }

        // Set Camera to Pin Board Spot
        if (PinBoardManager.Instance != null) PinBoardManager.Instance.SetVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (mainCamera != null && pinBoardCameraPos != null)
        {
            mainCamera.transform.position = pinBoardCameraPos.position;
            mainCamera.transform.rotation = pinBoardCameraPos.rotation;
        }


        yield return new WaitForSeconds(0.5f);
        isInputLocked = false;
    }

    private System.Collections.IEnumerator OpenPinBoard()
    {
        if (inputManager != null) inputManager.ForceReset();
        isInputLocked = true;
        
        stateBeforePinBoard = currentState; // Remember where we were
        currentState = GameState.PinBoard;

        // Ensure cursor is free for Pin Board
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Hide other UIs
        if (selectionManager != null) selectionManager.isInputActive = false;

        // Move Camera
        if (mainCamera != null && pinBoardCameraPos != null)
        {
            mainCamera.transform.position = pinBoardCameraPos.position;
            mainCamera.transform.rotation = pinBoardCameraPos.rotation;
        }

        // Show Pin Board UI
        if (PinBoardManager.Instance != null) PinBoardManager.Instance.SetVisible(true);

        yield return new WaitForSeconds(0.3f);
        isInputLocked = false;
    }

    private System.Collections.IEnumerator ClosePinBoard()
    {
        isInputLocked = true;

        // Hide Pin Board UI
        if (PinBoardManager.Instance != null) PinBoardManager.Instance.SetVisible(false);

        // Restore State
        if (stateBeforePinBoard == GameState.Interrogation)
        {
            // Move camera back to interrogation spot
            if (mainCamera != null && interrogationCameraPos != null)
            {
                mainCamera.transform.position = interrogationCameraPos.position;
                mainCamera.transform.rotation = interrogationCameraPos.rotation * Quaternion.Euler(0, 90, 0);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            currentState = GameState.Interrogation;
        }
        else if (stateBeforePinBoard == GameState.Accusation)
        {
            // Move camera back to pin board spot
            if (mainCamera != null && pinBoardCameraPos != null)
            {
                mainCamera.transform.position = pinBoardCameraPos.position;
                mainCamera.transform.rotation = pinBoardCameraPos.rotation;
            }
            currentState = GameState.Accusation;
        }
        else
        {
            // Default back to selection
            if (mainCamera != null && selectionManager != null && selectionManager.cameraPosition != null)
            {
                mainCamera.transform.position = selectionManager.cameraPosition.position;
                mainCamera.transform.rotation = selectionManager.cameraPosition.rotation * Quaternion.Euler(0, 180, 0);
            }
            if (selectionManager != null)
            {
                selectionManager.SetVisible(true);
                selectionManager.isInputActive = true;
            }
            currentState = GameState.SubjectSelection;
            
            // Ensure cursor is free for selection
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        yield return new WaitForSeconds(0.3f);
        isInputLocked = false;
    }
}
