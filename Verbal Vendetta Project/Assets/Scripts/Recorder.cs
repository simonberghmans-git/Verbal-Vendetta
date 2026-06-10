using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attached to a physical Recorder or Transcript item on the interrogation table.
/// Clicking this object logs the last suspect statement to the Pin Board.
/// </summary>
public class Recorder : MonoBehaviour
{
    [Header("Visual Feedback")]
    public AudioClip recordingClip;
    public Color materialColor = new Color(0, 0.8f, 1, 1); // Blue for recording
    public float highlightIntensity = 1.0f;
    private Animator recorderAnimator;

    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private bool isHovered = false;

    void Start()
    {
        recorderAnimator = GetComponentInChildren<Animator>();
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
        if (GameManager.Instance == null) return;
        
        var state = GameManager.Instance.currentState;
        // Only allow recording during interrogation
        if (state != GameManager.GameState.Interrogation)
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
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered) SetHover(true);

                if (Input.GetMouseButtonDown(0))
                {
                    DoRecord();
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

    private void DoRecord()
    {
        recorderAnimator.SetTrigger("Click");
        Debug.Log("[Recorder] Clicked! Recording last statement.");
        var intMan = FindAnyObjectByType<InterrogationManager>();
        if (intMan != null)
        {
            intMan.RecordLastStatement();
            AudioSource source = intMan.GetComponent<AudioSource>();
            if (source != null)
            {
                source.PlayOneShot(recordingClip);
            }
        }
    }
}
