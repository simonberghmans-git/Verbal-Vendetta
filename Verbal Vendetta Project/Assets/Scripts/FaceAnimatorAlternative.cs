using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Alternative version of FaceAnimator using a different set of blend shapes (ARKit/Standards variant).
/// </summary>
public class FaceAnimatorAlternative : MonoBehaviour
{
    public enum EmotionType { Neutral, Angry, Shocked, Sad, Smug, Nervous, Guilty, BrowLiftTest }

    [System.Serializable]
    public struct BlendShapeWeight
    {
        public string shapeName; 
        public float weight;
    }

    [Header("Components")]
    [Tooltip("The SkinnedMeshRenderer for the character's head.")]
    public SkinnedMeshRenderer headMesh;

    [Header("Settings")]
    public float transitionSpeed = 5f;
    
    // Internal dictionary for the hard-coded profiles
    private Dictionary<EmotionType, List<BlendShapeWeight>> defaultProfiles;
    private float[] targetWeights;

    private void Awake()
    {
        if (headMesh == null) headMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        if (headMesh != null && headMesh.sharedMesh != null)
        {
            targetWeights = new float[headMesh.sharedMesh.blendShapeCount];
        }
        
        InitializeDefaultProfiles();
        ResetToNeutral();
    }

    private void LateUpdate()
    {
        if (headMesh == null || headMesh.sharedMesh == null || targetWeights == null) return;

        for (int i = 0; i < headMesh.sharedMesh.blendShapeCount; i++)
        {
            float current = headMesh.GetBlendShapeWeight(i);
            float target = targetWeights[i];
            
            if (Mathf.Abs(current - target) > 0.1f)
            {
                float next = Mathf.Lerp(current, target, Time.deltaTime * transitionSpeed);
                headMesh.SetBlendShapeWeight(i, next);
            }
            else if (current != target)
            {
                headMesh.SetBlendShapeWeight(i, target);
            }
        }
    }

    private void InitializeDefaultProfiles()
    {
        defaultProfiles = new Dictionary<EmotionType, List<BlendShapeWeight>>();

        // --- ANGRY ---
        defaultProfiles[EmotionType.Angry] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Down_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Brow_Down_R", weight = 40 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_L", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_R", weight = 50 },
            new BlendShapeWeight { shapeName = "Nose_Wrinkle_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Nose_Wrinkle_R", weight = 60 },
            new BlendShapeWeight { shapeName = "Nose_Nostril_Dilate_L", weight = 90 },
            new BlendShapeWeight { shapeName = "Nose_Nostril_Dilate_R", weight = 90 },

        };

        // --- SHOCKED ---
        defaultProfiles[EmotionType.Shocked] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_In_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_In_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Widen_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Widen_R", weight = 80 },

        };

        // --- SAD ---
        defaultProfiles[EmotionType.Sad] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_In_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_In_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Mouth_Corner_Depress_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Mouth_Corner_Depress_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Blink_L", weight = 15 },
            new BlendShapeWeight { shapeName = "Eye_Blink_R", weight = 15 },
            new BlendShapeWeight { shapeName = "Mouth_Lips_Push_DL", weight = 50 }, 
            new BlendShapeWeight { shapeName = "Mouth_Lips_Push_DR", weight = 50 } 
        };

        // --- SMUG ---
        defaultProfiles[EmotionType.Smug] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Mouth_Corner_Pull_L", weight = 70 },
            new BlendShapeWeight { shapeName = "Mouth_Corner_Pull_R", weight = 10 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_L", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_R", weight = 50 },
            new BlendShapeWeight { shapeName = "Mouth_Dimple_L", weight = 40 }
        };

        // --- NERVOUS ---
        defaultProfiles[EmotionType.Nervous] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_R", weight = 60 },
            new BlendShapeWeight { shapeName = "Mouth_Lips_Press_L", weight = 70 },
            new BlendShapeWeight { shapeName = "Mouth_Lips_Press_R", weight = 70 },
            new BlendShapeWeight { shapeName = "Brow_Lateral_L", weight = 50 }, 
            new BlendShapeWeight { shapeName = "Brow_Lateral_R", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Widen_L", weight = 20 },
            new BlendShapeWeight { shapeName = "Eye_Widen_R", weight = 20 }
        };

        // --- GUILTY ---
        defaultProfiles[EmotionType.Guilty] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_In_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_In_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Squint_Inner_R", weight = 80 },
            new BlendShapeWeight { shapeName = "Mouth_Lips_Press_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Lips_Press_R", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Corner_Depress_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Corner_Depress_R", weight = 40 },
            new BlendShapeWeight { shapeName = "Brow_Down_R", weight = 30 }
        };

        // --- BROW LIFT TEST ---
        defaultProfiles[EmotionType.BrowLiftTest] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_In_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_In_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 100 }
        };
    }

    public void SetEmotion(string emotion)
    {
        Debug.Log($"FaceAnimatorAlternative: Setting emotion to {emotion}");
        EmotionType type = ParseEmotion(emotion);
        
        if (headMesh == null || headMesh.sharedMesh == null || targetWeights == null) return;

        // Clear target weights
        for (int i = 0; i < targetWeights.Length; i++)
        {
            targetWeights[i] = 0f;
        }

        // Apply new target blendshapes
        if (defaultProfiles != null && defaultProfiles.ContainsKey(type))
        {
            foreach (var pose in defaultProfiles[type])
            {
                int index = headMesh.sharedMesh.GetBlendShapeIndex(pose.shapeName);
                if (index != -1)
                {
                    targetWeights[index] = pose.weight;
                }
            }
        }
    }

    public void ResetToNeutral() => SetEmotion("Neutral");

    public static EmotionType ParseEmotion(string emotionName)
    {
        if (System.Enum.TryParse<EmotionType>(emotionName, true, out EmotionType result))
        {
            return result;
        }
        Debug.LogWarning($"FaceAnimatorAlternative: Unknown emotion '{emotionName}', falling back to Neutral.");
        return EmotionType.Neutral;
    }
}
