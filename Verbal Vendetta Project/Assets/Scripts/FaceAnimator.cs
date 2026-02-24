using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages HD Facial Emotions for CC4/CC5 characters.
/// Hard-coded with refined technical recommendations for L/R blendshapes.
/// Fixed: Re-calibrated 'Angry' for aggressive grit and 'Sad' for deeper sorrow.
/// </summary>
public class FaceAnimator : MonoBehaviour
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

    // Internal dictionary for the hard-coded profiles
    private Dictionary<EmotionType, List<BlendShapeWeight>> defaultProfiles;

    private void Awake()
    {
        if (headMesh == null) headMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        InitializeDefaultProfiles();
        ResetToNeutral();
    }

    private void InitializeDefaultProfiles()
    {
        defaultProfiles = new Dictionary<EmotionType, List<BlendShapeWeight>>();

        // --- ANGRY (Updated: Maximum furrow and lip pressure) ---
        defaultProfiles[EmotionType.Angry] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Drop_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Brow_Drop_R", weight = 40 },
            // Eyes: Eye Squint (50%) + Lower Lid Raise (40%)
            new BlendShapeWeight { shapeName = "Eye_Squint_L", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Squint_R", weight = 50 },
            
            // Nose: Nose Wrinkle (60%) + Nostril Flare (90%)
            new BlendShapeWeight { shapeName = "Nose_Sneer_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Nose_Sneer_R", weight = 60 },
            new BlendShapeWeight { shapeName = "Nose_Nostril_Dilate_L", weight = 90 },
            new BlendShapeWeight { shapeName = "Nose_Nostril_Dilate_R", weight = 90 },
            

        };

        // --- SHOCKED ---
        defaultProfiles[EmotionType.Shocked] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Wide_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Wide_R", weight = 80 },

        };

        // --- SAD (Updated: Droopy lids and chin shrug) ---
        defaultProfiles[EmotionType.Sad] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Mouth_Frown_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Mouth_Frown_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Mouth_Shrug_Lower", weight = 50 }, // Pouting the chin upward
            new BlendShapeWeight { shapeName = "Eye_Blink_L", weight = 15 }, // Slight lid closure
            new BlendShapeWeight { shapeName = "Eye_Blink_R", weight = 15 }
        };

        // --- SMUG (Asymmetric) ---
        defaultProfiles[EmotionType.Smug] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Mouth_Smile_L", weight = 70 },
            new BlendShapeWeight { shapeName = "Mouth_Smile_R", weight = 10 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Eye_Squint_L", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Squint_R", weight = 50 },
            new BlendShapeWeight { shapeName = "Mouth_Dimple_L", weight = 40 }
        };

        // --- NERVOUS ---
        defaultProfiles[EmotionType.Nervous] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Squint_L", weight = 60 },
            new BlendShapeWeight { shapeName = "Eye_Squint_R", weight = 60 },
            new BlendShapeWeight { shapeName = "Mouth_Press_L", weight = 70 },
            new BlendShapeWeight { shapeName = "Mouth_Press_R", weight = 70 },
            new BlendShapeWeight { shapeName = "Brow_Compress_L", weight = 50 },
            new BlendShapeWeight { shapeName = "Brow_Compress_R", weight = 50 },
            new BlendShapeWeight { shapeName = "Eye_Wide_L", weight = 20 },
            new BlendShapeWeight { shapeName = "Eye_Wide_R", weight = 20 }
        };

        // --- GUILTY ---
        defaultProfiles[EmotionType.Guilty] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Squint_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Squint_R", weight = 80 },
            new BlendShapeWeight { shapeName = "Mouth_Press_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Press_R", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Frown_L", weight = 40 },
            new BlendShapeWeight { shapeName = "Mouth_Frown_R", weight = 40 },
            new BlendShapeWeight { shapeName = "Brow_Drop_R", weight = 30 }
        };

        // --- BROW LIFT TEST (Wrinkle Check) ---
        defaultProfiles[EmotionType.BrowLiftTest] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Inner_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 100 }
        };
    }

    public void SetEmotion(string emotion)
    {
        Debug.Log($"FaceAnimator: Setting emotion to {emotion}");
        EmotionType type = ParseEmotion(emotion);
        
        if (headMesh == null || headMesh.sharedMesh == null) return;

        // Reset all active blendshapes to 0
        for (int i = 0; i < headMesh.sharedMesh.blendShapeCount; i++)
        {
            headMesh.SetBlendShapeWeight(i, 0f);
        }

        // Apply new target blendshapes
        if (defaultProfiles != null && defaultProfiles.ContainsKey(type))
        {
            foreach (var pose in defaultProfiles[type])
            {
                int index = headMesh.sharedMesh.GetBlendShapeIndex(pose.shapeName);
                if (index != -1)
                {
                    headMesh.SetBlendShapeWeight(index, pose.weight);
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
        Debug.LogWarning($"FaceAnimator: Unknown emotion '{emotionName}', falling back to Neutral.");
        return EmotionType.Neutral;
    }
}