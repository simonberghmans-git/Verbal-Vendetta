using UnityEngine;

/// <summary>
/// A simple testing utility to switch between FaceAnimator emotions in the inspector.
/// Attach this to the suspect prefab or the object containing the FaceAnimator component.
/// </summary>
public class FaceAnimationsTest : MonoBehaviour
{
    [Header("Target")]
    public FaceAnimator faceAnimator;

    [Header("Testing Controls")]
    [Tooltip("Change this value in the Inspector while the game is running to test blendshape profiles.")]
    public FaceAnimator.EmotionType testEmotion;

    private FaceAnimator.EmotionType lastEmotion;

    private void Start()
    {
        // Auto-assign if not set manually
        if (faceAnimator == null)
        {
            faceAnimator = GetComponent<FaceAnimator>();
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
            Debug.Log($"FaceAnimationsTest: Applied {testEmotion} profile.");
        }
    }

    [ContextMenu("Reset to Neutral")]
    public void ResetToNeutral()
    {
        testEmotion = FaceAnimator.EmotionType.Neutral;
        if (faceAnimator != null) faceAnimator.ResetToNeutral();
    }
}