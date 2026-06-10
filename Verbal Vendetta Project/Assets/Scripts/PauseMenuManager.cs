using UnityEngine;

/// <summary>
/// Simple pause menu toggled by Escape.
/// Does not stop time — just toggles UI panels.
/// Attach to a persistent GameObject in the scene.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    [Header("Panels")]
    [Tooltip("The root pause menu panel (Resume / Exit / Settings buttons).")]
    public GameObject pauseMenuPanel;

    [Tooltip("The settings sub-panel (toggled by the Settings button).")]
    public GameObject settingsPanel;

    private bool pauseMenuHiddenForSettings = false;

    /// <summary>
    /// True while the pause menu is visible. Other scripts can check this
    /// to suppress their own input handling.
    /// </summary>
    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Make sure both panels start hidden
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        IsPaused = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If settings sub-panel is open, close it first and restore the pause menu.
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }

            // Otherwise toggle the main pause menu
            TogglePauseMenu();
        }
    }

    // ──────────────────────────────────────────────
    //  Button callbacks (wire these in the Inspector)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Hides the pause menu. Same effect as pressing Escape.
    /// </summary>
    public void Resume()
    {
        SetPauseMenu(false);
    }

    /// <summary>
    /// Returns to the main-menu scene (scene index 0).
    /// </summary>
    public void ExitToMenu()
    {
        SetPauseMenu(false);

        ScenesManager scenesManager = FindAnyObjectByType<ScenesManager>();
        if (scenesManager != null)
        {
            scenesManager.GoToMenu();
        }
        else
        {
            // Fallback: load scene 0 directly
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(0);
        }
    }

    /// <summary>
    /// Toggles the settings sub-panel on / off.
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsPanel == null || pauseMenuPanel == null)
        {
            return;
        }

        if (settingsPanel.activeSelf)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    private void OpenSettings()
    {
        if (settingsPanel == null || pauseMenuPanel == null)
        {
            return;
        }

        pauseMenuHiddenForSettings = pauseMenuPanel.activeSelf;
        settingsPanel.SetActive(true);
        pauseMenuPanel.SetActive(false);
    }

    private void CloseSettings()
    {
        if (settingsPanel == null || pauseMenuPanel == null)
        {
            return;
        }

        settingsPanel.SetActive(false);

        if (pauseMenuHiddenForSettings)
        {
            pauseMenuPanel.SetActive(true);
            pauseMenuHiddenForSettings = false;
        }
    }

    // ──────────────────────────────────────────────
    //  Internal helpers
    // ──────────────────────────────────────────────

    private void TogglePauseMenu()
    {
        SetPauseMenu(!IsPaused);
    }

    private void SetPauseMenu(bool open)
    {
        IsPaused = open;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(open);

        // Always close the settings sub-panel when closing the menu
        if (!open && settingsPanel != null) settingsPanel.SetActive(false);

        // Cursor handling and camera look script toggling
        if (open)
        {
            // Pause opened: unlock cursor and disable camera rotation
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Disable InterrogationCameraLook if present on main camera
            if (GameManager.Instance != null && GameManager.Instance.mainCamera != null)
            {
                var camLook = GameManager.Instance.mainCamera.GetComponent<InterrogationCameraLook>();
                if (camLook != null) camLook.enabled = false;
            }
        }
        else
        {
            // Pause closed: if still in interrogation, lock cursor and enable camera rotation
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.Interrogation)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                var camLook = GameManager.Instance.mainCamera.GetComponent<InterrogationCameraLook>();
                if (camLook != null) camLook.enabled = true;
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
                pauseMenuHiddenForSettings = false;
            }
        }
    }
}
