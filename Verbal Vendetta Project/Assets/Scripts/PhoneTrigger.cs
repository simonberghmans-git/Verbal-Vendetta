using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dedicated trigger for the Phone in the Pin Board scene.
/// Works exactly like the original handcuffs but specifically for the Pin Board state.
/// </summary>
public class PhoneTrigger : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color highlightColor = new Color(1, 0.8f, 0, 1); // Gold/Yellow
    public float highlightIntensity = 1.0f;

    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private bool isHovered = false;

    void Start()
    {
        // Cache renderers and original colors for highlighting
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
        if (GameManager.Instance == null) return;
        
        // Active in Pin Board or Accusation phases
        var state = GameManager.Instance.currentState;
        if (state != GameManager.GameState.PinBoard && state != GameManager.GameState.Accusation)
        {
            if (isHovered) SetHover(false);
            return;
        }

        // Use the dedicated main camera from GameManager
        Camera cam = GameManager.Instance.mainCamera != null ? GameManager.Instance.mainCamera : Camera.main;
        if (cam == null) return;

        // Raycast from mouse position (cursor is always unlocked in Pin Board and Accusation)
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered) SetHover(true);

                if (Input.GetMouseButtonDown(0))
                {
                    ToggleAccusation();
                }
            }
            else
            {
                if (isHovered) SetHover(false);
            }
        }
        else
        {
            if (isHovered) SetHover(false);
        }
    }

    private void SetHover(bool hovered)
    {
        isHovered = hovered;
        UpdateVisuals(hovered);
        if (hovered)
        {
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.Accusation)
            {
                TooltipManager.Show("Left Click to Hang Up");
            }
            else
            {
                TooltipManager.Show("Left Click to Call Police Chief");
            }
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
                        mat.SetColor("_BaseColor", highlightColor * highlightIntensity);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", highlightColor * highlightIntensity);
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

    private void ToggleAccusation()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentState == GameManager.GameState.Accusation)
        {
            Debug.Log("[PhoneTrigger] Hanging up. Returning to Pin Board.");
            GameManager.Instance.StopAccusationPhase();
        }
        else
        {
            Debug.Log("[PhoneTrigger] Calling Police Chief.");
            GameManager.Instance.StartAccusationPhase();
        }
        
        // Play pick up sound from InterrogationManager
        var intMan = FindObjectOfType<InterrogationManager>();
        if (intMan != null && intMan.accusationTriggerClip != null)
        {
            AudioSource source = intMan.GetComponent<AudioSource>();
            if (source != null)
            {
                source.PlayOneShot(intMan.accusationTriggerClip);
            }
        }
    }
}
