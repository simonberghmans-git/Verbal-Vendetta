using System.Collections;
using UnityEngine;

public class EyeBlinking : MonoBehaviour
{
    [Header("Model Settings")]
    [Tooltip("The SkinnedMeshRenderer containing the blend shapes.")]
    public SkinnedMeshRenderer targetMeshRenderer;
    
    [Tooltip("The name of the left eye blink blend shape.")]
    public string leftEyeBlinkName = "Eye_Blink_L";
    
    [Tooltip("The name of the right eye blink blend shape.")]
    public string rightEyeBlinkName = "Eye_Blink_R";

    [Header("Blink Settings")]
    [Tooltip("Minimum time (in seconds) between blinks.")]
    public float minBlinkInterval = 2.0f;
    
    [Tooltip("Maximum time (in seconds) between blinks.")]
    public float maxBlinkInterval = 5.0f;
    
    [Tooltip("Duration of the closing phase of the blink.")]
    public float closeDuration = 0.05f;
    
    [Tooltip("Duration of the opening phase of the blink.")]
    public float openDuration = 0.1f;

    private int leftEyeIndex = -1;
    private int rightEyeIndex = -1;
    private Coroutine blinkCoroutine;

    void Start()
    {
        if (targetMeshRenderer == null)
        {
            targetMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (targetMeshRenderer != null)
        {
            leftEyeIndex = targetMeshRenderer.sharedMesh.GetBlendShapeIndex(leftEyeBlinkName);
            rightEyeIndex = targetMeshRenderer.sharedMesh.GetBlendShapeIndex(rightEyeBlinkName);

            if (leftEyeIndex == -1) Debug.LogWarning($"EyeBlinking: Blend shape '{leftEyeBlinkName}' not found.");
            if (rightEyeIndex == -1) Debug.LogWarning($"EyeBlinking: Blend shape '{rightEyeBlinkName}' not found.");

            blinkCoroutine = StartCoroutine(BlinkRoutine());
        }
        else
        {
            Debug.LogError("EyeBlinking: No SkinnedMeshRenderer assigned or found.");
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            // Wait for a random interval before blinking
            float waitTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            yield return new WaitForSeconds(waitTime);

            // Close eyes
            yield return StartCoroutine(AnimateBlink(0f, 100f, closeDuration));

            // Small pause while eyes are closed (optional, very short)
            // yield return new WaitForSeconds(0.01f);

            // Open eyes
            yield return StartCoroutine(AnimateBlink(100f, 0f, openDuration));
        }
    }

    private IEnumerator AnimateBlink(float startWeight, float endWeight, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentWeight = Mathf.Lerp(startWeight, endWeight, t);

            if (leftEyeIndex != -1) targetMeshRenderer.SetBlendShapeWeight(leftEyeIndex, currentWeight);
            if (rightEyeIndex != -1) targetMeshRenderer.SetBlendShapeWeight(rightEyeIndex, currentWeight);

            yield return null;
        }

        // Ensure final weight is set exactly
        if (leftEyeIndex != -1) targetMeshRenderer.SetBlendShapeWeight(leftEyeIndex, endWeight);
        if (rightEyeIndex != -1) targetMeshRenderer.SetBlendShapeWeight(rightEyeIndex, endWeight);
    }

    void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
    }
}
