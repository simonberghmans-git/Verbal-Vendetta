using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attached to the Handcuffs or Phone on the interrogation table.
/// Handles both the visual hover highlighting and the accusation trigger logic.
/// </summary>
public class Handcuffs : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color materialColor = new Color(1, 0.8f, 0, 1); // Gold/Yellow for accusation
    public float highlightIntensity = 1.0f;

    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private bool isHovered = false;

    void Start()
    {
        // Cache renderers and original material colors
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                // Cache Base Color (HDRP/URP/Standard)
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
        
        var state = GameManager.Instance.currentState;
        if (state != GameManager.GameState.SubjectSelection && state != GameManager.GameState.Interrogation)
        {
            if (isHovered) SetHover(false);
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
        // Raycast with a distance of 100 units
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered) SetHover(true);

                if (Input.GetMouseButtonDown(0))
                {
                    TriggerAccusation();
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

    private void TriggerAccusation()
    {
        Debug.Log("[Handcuffs] Clicked! Starting Accusation Phase.");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartAccusationPhase();
        }
        
        // Play pick up sound
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
