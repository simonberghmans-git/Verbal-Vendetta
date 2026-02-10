using UnityEngine;
using System.Collections.Generic;

public class JawSync : MonoBehaviour
{
    [Header("Setup")]
    public SkinnedMeshRenderer targetMesh;  // Your Character's Face Mesh
    public Transform jawBone;               // The 'CC_Base_JawRoot' bone

    [Header("Configuration")]
    // List the blend shapes that should move the jaw (e.g., V_Open, V_Wide, V_Tight_O)
    public List<string> blendShapesToWatch = new List<string> { "V_Open", "V_Wide", "V_Lip_Open", "V_Tight_O" };

    [Header("Tuning")]
    [Range(0, 3)] public float sensitivity = 1.0f; // Multiplier for how much the jaw moves
    [Range(0, 20)] public float smoothness = 10.0f; // Higher = Snappier, Lower = Smoother

    [Header("Rotation Limits")]
    // The rotation added when the mouth is fully OPEN
    public Vector3 openRotation = new Vector3(15, 0, 0); 
    
    // The rotation applied when the mouth is CLOSED (Negative helps seal the lips)
    public Vector3 closeOffset = new Vector3(-2, 0, 0); 

    void LateUpdate()
    {
        if (targetMesh == null || jawBone == null) return;

        // 1. Find the maximum weight among all watched blend shapes
        float maxWeight = 0f;
        foreach (string shapeName in blendShapesToWatch)
        {
            int index = targetMesh.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index != -1)
            {
                float currentWeight = targetMesh.GetBlendShapeWeight(index);
                if (currentWeight > maxWeight)
                {
                    maxWeight = currentWeight;
                }
            }
        }

        // 2. Normalize weight (0 to 1)
        float normalizedWeight = maxWeight / 100f;

        // 3. Calculate Target Rotation
        // Formula: Base Offset + (Max Open Angle * Current Weight * Sensitivity)
        Vector3 currentTargetEuler = closeOffset + (openRotation * normalizedWeight * sensitivity);
        Quaternion targetRotation = Quaternion.Euler(currentTargetEuler);

        // 4. Apply Rotation with smoothing (Slerp) to reduce robotic jitter
        jawBone.localRotation = Quaternion.Slerp(jawBone.localRotation, targetRotation, Time.deltaTime * smoothness);
    }
}