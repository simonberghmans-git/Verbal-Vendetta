using UnityEngine;

/// <summary>
/// A simple testing utility to switch between FaceAnimatorAlternative emotions in the inspector.
/// Attach this to the suspect prefab or the object containing the FaceAnimatorAlternative component.
/// </summary>
public class FaceAnimationsTestAlternative : MonoBehaviour
{
    [Header("Target")]
    public FaceAnimatorAlternative faceAnimator;

    [Header("Testing Controls")]
    [Tooltip("Change this value in the Inspector while the game is running to test blendshape profiles.")]
    public FaceAnimatorAlternative.EmotionType testEmotion;

    private FaceAnimatorAlternative.EmotionType lastEmotion;

    private void Start()
    {
        // Auto-assign if not set manually
        if (faceAnimator == null)
        {
            faceAnimator = GetComponent<FaceAnimatorAlternative>();
        }

        // Synchronize initial state
        if (faceAnimator != null)
        {
            lastEmotion = testEmotion;
            faceAnimator.SetEmotion(testEmotion.ToString());
        }
    }

    private void Update()
    {
        if (faceAnimator == null) return;

        // Check if the emotion was changed in the Unity Inspector
        if (testEmotion != lastEmotion)
        {
            faceAnimator.SetEmotion(testEmotion.ToString());
            lastEmotion = testEmotion;
            Debug.Log($"FaceAnimationsTestAlternative: Applied {testEmotion} profile.");
        }
    }

    [ContextMenu("Reset to Neutral")]
    public void ResetToNeutral()
    {
        testEmotion = FaceAnimatorAlternative.EmotionType.Neutral;
        if (faceAnimator != null) faceAnimator.ResetToNeutral();
    }
}
