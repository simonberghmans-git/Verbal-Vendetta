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
    public float transitionSpeed = 3f;
    
    // Internal dictionary for the hard-coded profiles
    private Dictionary<EmotionType, List<BlendShapeWeight>> defaultProfiles;
    private Dictionary<int, float> targetWeights = new Dictionary<int, float>();
    private Coroutine speechTransitionCoroutine;

    private void Awake()
    {
        if (headMesh == null) headMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        InitializeDefaultProfiles();
        ResetToNeutral();
    }

    [Header("Debug")]
    public EmotionType debugEmotion;
    public bool applyDebugEmotion;

    private void LateUpdate()
    {
        ApplyBlendShapes();

        if (applyDebugEmotion)
        {
            applyDebugEmotion = false;
            SetEmotion(debugEmotion);
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
            new BlendShapeWeight { shapeName = "Jaw_Open", weight = 25 },
            new BlendShapeWeight { shapeName = "V_Lip_Open", weight = 15 }
        };

        // --- SHOCKED ---
        defaultProfiles[EmotionType.Shocked] = new List<BlendShapeWeight> {
            new BlendShapeWeight { shapeName = "Brow_Raise_In_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_In_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_L", weight = 100 },
            new BlendShapeWeight { shapeName = "Brow_Raise_Outer_R", weight = 100 },
            new BlendShapeWeight { shapeName = "Eye_Widen_L", weight = 80 },
            new BlendShapeWeight { shapeName = "Eye_Widen_R", weight = 80 },
            new BlendShapeWeight { shapeName = "Jaw_Open", weight = 25 },
            new BlendShapeWeight { shapeName = "V_Lip_Open", weight = 15 }
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

    private void ApplyBlendShapes()
    {
        if (headMesh == null) return;

        foreach (var entry in targetWeights)
        {
            float current = headMesh.GetBlendShapeWeight(entry.Key);
            float next = Mathf.Lerp(current, entry.Value, Time.deltaTime * transitionSpeed);
            headMesh.SetBlendShapeWeight(entry.Key, next);
        }
    }

    public void SetEmotion(EmotionType type)
    {
        if (speechTransitionCoroutine != null) StopCoroutine(speechTransitionCoroutine);
        UpdateTargetWeights(type);
    }

    public void PlaySpeechEmotions(EmotionType startEmotion, EmotionType endEmotion, float duration)
    {
        if (speechTransitionCoroutine != null) StopCoroutine(speechTransitionCoroutine);
        speechTransitionCoroutine = StartCoroutine(SpeechTransitionRoutine(startEmotion, endEmotion, duration));
    }

    private IEnumerator SpeechTransitionRoutine(EmotionType start, EmotionType end, float duration)
    {
        UpdateTargetWeights(start);
        float transitionPoint = duration * 0.4f;
        yield return new WaitForSeconds(transitionPoint);
        UpdateTargetWeights(end);
        speechTransitionCoroutine = null;
    }

    private void UpdateTargetWeights(EmotionType type)
    {
        if (defaultProfiles == null || !defaultProfiles.ContainsKey(type))
        {
            List<int> currentKeys = new List<int>(targetWeights.Keys);
            foreach (int key in currentKeys) targetWeights[key] = 0f;
            return;
        }

        List<BlendShapeWeight> activePoses = defaultProfiles[type];
        
        List<int> keys = new List<int>(targetWeights.Keys);
        foreach (int key in keys) targetWeights[key] = 0f;

        foreach (var pose in activePoses)
        {
            int index = headMesh.sharedMesh.GetBlendShapeIndex(pose.shapeName);
            if (index != -1)
            {
                targetWeights[index] = pose.weight;
            }
        }
    }

    public void ResetToNeutral() => SetEmotion(EmotionType.Neutral);

    public static EmotionType ParseEmotion(string emotionName)
    {
        if (System.Enum.TryParse<EmotionType>(emotionName, true, out EmotionType result))
        {
            return result;
        }
        return EmotionType.Neutral;
    }
}
