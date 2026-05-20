using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to any in‑scene item (e.g., a tablet, a button, a decorative object) that should
/// open the Pin Board UI when the player clicks it. The behavior mirrors the
/// existing Recorder and Handcuffs interactables but simply triggers the Pin Board.
/// Includes visual hover feedback (color change).
/// </summary>
public class PinBoardTrigger : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color materialColor = new Color(0, 0.8f, 1, 1); // Cyan highlight
    public float highlightIntensity = 1.0f;

    private bool isHovered = false;
    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();

    void Start()
    {
        // Cache renderers and original colors
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
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
            ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        else
            ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered) SetHover(true);
                if (Input.GetMouseButtonDown(0))
                {
                    OpenPinBoard();
                }
            }
            else if (isHovered)
            {
                SetHover(false);
            }
        }
        else if (isHovered)
        {
            SetHover(false);
        }
    }

    private void SetHover(bool hovered)
    {
        isHovered = hovered;
        UpdateVisuals(hovered);
        if (hovered)
        {
            TooltipManager.Show("Press Left Click to go to the Pin Board");
        }
        else
        {
            TooltipManager.Hide();
        }
    }

    private void UpdateVisuals(bool highlighted)
    {
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
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

        private void OpenPinBoard()
        {
            Debug.Log("OpenPinBoard");
            if (GameManager.Instance != null)
            {
                // Start the coroutine to open the pin board UI
                GameManager.Instance.StartCoroutine(GameManager.Instance.OpenPinBoard());
            }
        }
}