using UnityEngine;
using System.Collections.Generic;

public class SuspectHighlight : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color materialColor = new Color(0, 0.5f, 0.5f, 1); // Subtle teal
    public float highlightIntensity = 0.5f;
    
    private Renderer[] renderers;
    private Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    private bool isSelected = false;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        
        // Cache original values
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                // Cache Base Color (HDRP/URP/Standard)
                if (mat.HasProperty("_BaseColor"))
                {
                    originalColors[mat] = mat.GetColor("_BaseColor");
                }
                else if (mat.HasProperty("_Color"))
                {
                    originalColors[mat] = mat.GetColor("_Color");
                }
                else
                {
                    originalColors[mat] = Color.white;
                }
            }
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (isSelected)
                {
                    // Apply Material Color
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", materialColor * highlightIntensity);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", materialColor * highlightIntensity);
                }
                else
                {
                    // Restore original
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

        // --- Simple Fallback: Scale Pulse ---
        // Removed as requested
    }
}
