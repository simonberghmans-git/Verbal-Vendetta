using UnityEngine;
using System.Collections.Generic;

public class SuspectHighlight : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color highlightColor = new Color(0, 0.5f, 0.5f, 1); // Subtle teal
    public float highlightIntensity = 0.5f;
    
    private Renderer[] renderers;
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    private bool isSelected = false;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        
        // Cache original values
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                // Cache Emissive Color (HDRP)
                if (mat.HasProperty("_EmissiveColor"))
                {
                    originalEmissionColors[mat] = mat.GetColor("_EmissiveColor");
                }
                // Cache Emission Color (Standard)
                else if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
                }
                else
                {
                    originalEmissionColors[mat] = Color.black;
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
                    mat.EnableKeyword("_EMISSION");
                    
                    // Try both property names for maximum compatibility
                    if (mat.HasProperty("_EmissiveColor"))
                        mat.SetColor("_EmissiveColor", highlightColor * highlightIntensity);
                    
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", highlightColor * highlightIntensity);
                }
                else
                {
                    // Restore original
                    if (originalEmissionColors.ContainsKey(mat))
                    {
                        if (mat.HasProperty("_EmissiveColor"))
                            mat.SetColor("_EmissiveColor", originalEmissionColors[mat]);
                        
                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", originalEmissionColors[mat]);

                        if (originalEmissionColors[mat] == Color.black)
                        {
                            mat.DisableKeyword("_EMISSION");
                        }
                    }
                }
            }
        }

        // --- Simple Fallback: Scale Pulse ---
        // Removed as requested
    }
}
