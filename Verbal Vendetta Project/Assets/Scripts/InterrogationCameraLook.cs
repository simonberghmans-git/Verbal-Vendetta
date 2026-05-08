using UnityEngine;

/// <summary>
/// Allows the camera to rotate slightly based on mouse movement during the Interrogation phase.
/// </summary>
public class InterrogationCameraLook : MonoBehaviour
{
    [Header("Dependencies")]
    public GameManager gameManager;

    [Header("Rotation Limits")]
    public float maxHorizontalAngle = 45f;
    public float maxVerticalAngle = 20f;
    public float sensitivity = 2f;
    public float smoothTime = 5f;

    // Internal State
    private float rotationX = 0f;
    private float rotationY = 0f;
    private Quaternion baseRotation;
    private GameManager.GameState lastState;
    private bool wasInInterrogation = false;

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        lastState = gameManager != null ? gameManager.currentState : GameManager.GameState.SubjectSelection;
    }

    void LateUpdate()
    {
        if (gameManager == null) return;

        bool isInterrogating = gameManager.currentState == GameManager.GameState.Interrogation;

        // Detect State Entry/Exit
        if (isInterrogating && !wasInInterrogation)
        {
            // Just entered Interrogation phase, but wait for the transition (camera move) to finish
        }
        else if (!isInterrogating && wasInInterrogation)
        {
            // Just left Interrogation
            OnExitInterrogation();
        }

        // Only capture the base rotation and allow movement once the GameManager has finished moving the camera
        if (isInterrogating && !gameManager.isInputLocked && !hasCapturedBase)
        {
            OnEnterInterrogation();
        }

        wasInInterrogation = isInterrogating;

        if (isInterrogating && hasCapturedBase)
        {
            HandleMouseLook();
        }
    }

    private bool hasCapturedBase = false;

    private void OnEnterInterrogation()
    {
        // Store the rotation set by GameManager as the base
        baseRotation = transform.rotation;
        rotationX = 0f;
        rotationY = 0f;
        hasCapturedBase = true;
        Debug.Log("[CameraLook] Base rotation captured. Mouse-look active.");
    }

    private void OnExitInterrogation()
    {
        // Reset local offsets
        rotationX = 0f;
        rotationY = 0f;
        hasCapturedBase = false;
    }

    private void HandleMouseLook()
    {
        // Capture Mouse Input
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // Update target offsets
        rotationY += mouseX;
        rotationX -= mouseY;

        // Clamp offsets
        rotationY = Mathf.Clamp(rotationY, -maxHorizontalAngle, maxHorizontalAngle);
        rotationX = Mathf.Clamp(rotationX, -maxVerticalAngle, maxVerticalAngle);

        // Apply rotation relative to the base rotation
        Quaternion targetRotation = baseRotation * Quaternion.Euler(rotationX, rotationY, 0f);
        
        // Smoothly interpolate to target
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothTime);
    }
}
