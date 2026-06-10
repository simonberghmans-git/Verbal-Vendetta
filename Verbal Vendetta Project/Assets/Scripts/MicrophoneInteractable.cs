using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attached to a physical Microphone on the interrogation table.
/// Holding click on this object triggers the InterrogationInputManager to start recording.
/// </summary>
public class MicrophoneInteractable : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color materialColor = new Color(1, 0, 0, 1); // Red for recording
    public float highlightIntensity = 1.0f;
    
    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private bool isHovered = false;
    private bool isPressed = false;
    
    private InterrogationInputManager inputManager;

    void Start()
    {
        inputManager = FindAnyObjectByType<InterrogationInputManager>();
        
        // Cache renderers and original material colors
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                    originalColors[mat] = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    originalColors[mat] = mat.GetColor("_Color");
                else
                    originalColors[mat] = Color.white;
            }
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || inputManager == null) return;
        
        var state = GameManager.Instance.currentState;
        // Only allow microphone during interrogation
        if (state != GameManager.GameState.Interrogation)
        {
            if (isHovered) SetHover(false);
            if (isPressed) ReleaseMic();
            return;
        }

        // Block input if Pin Board is open
        if (PinBoardManager.Instance != null && PinBoardManager.Instance.IsOpen)
        {
            if (isHovered) SetHover(false);
            if (isPressed) ReleaseMic();
            return;
        }

        Camera cam = GameManager.Instance.mainCamera != null ? GameManager.Instance.mainCamera : Camera.main;
        if (cam == null) return;

        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        }
        else
        {
            ray = cam.ScreenPointToRay(Input.mousePosition);
        }

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered && !isPressed) SetHover(true);

                if (Input.GetMouseButtonDown(0))
                {
                    PressMic();
                }
            }
            else
            {
                if (isHovered && !isPressed) SetHover(false);
            }
        }
        else
        {
            if (isHovered && !isPressed) SetHover(false);
        }

        if (isPressed && Input.GetMouseButtonUp(0))
        {
            ReleaseMic();
        }
    }

    private void SetHover(bool hovered)
    {
        isHovered = hovered;
        UpdateVisuals(hovered);
        if (hovered)
        {
            TooltipManager.Show("Hold Left Click to Speak");
        }
        else
        {
            TooltipManager.Hide();
        }
    }

    private void UpdateVisuals(bool highlighted)
    {
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (highlighted)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", materialColor * highlightIntensity);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", materialColor * highlightIntensity);
                }
                else
                {
                    if (originalColors.ContainsKey(mat))
                    {
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", originalColors[mat]);
                        else if (mat.HasProperty("_Color"))
                            mat.SetColor("_Color", originalColors[mat]);
                    }
                }
            }
        }
    }

    private void PressMic()
    {
        if (isPressed) return;
        isPressed = true;
        Debug.Log("[MicrophoneInteractable] Pressed! Starting recording.");
        UpdateVisuals(true);
        inputManager.StartRecording();
    }

    private void ReleaseMic()
    {
        if (!isPressed) return;
        isPressed = false;
        Debug.Log("[MicrophoneInteractable] Released! Stopping recording.");
        UpdateVisuals(isHovered);
        inputManager.StopRecording();
    }
}
