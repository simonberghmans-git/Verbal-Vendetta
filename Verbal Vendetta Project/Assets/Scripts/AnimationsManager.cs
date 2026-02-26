using UnityEngine;
using System.Collections;

/// <summary>
/// The "Brain" of the animation system.
/// Holds the global stress state and calculates idle variety.
/// This script does NOT need a reference to any specific Animator.
/// </summary>
public class AnimationsManager : MonoBehaviour
{
    public static AnimationsManager Instance { get; private set; }

    [Header("Global Stress State")]
    [Range(0f, 1f)] public float stressLevel = 0f;
    
    [Header("Playback Speed Rules")]
    public float baseSpeed = 1f;
    public float maxStressSpeedMultiplier = 1.5f;



    // Reference to the currently active suspect's Animator
    private Animator currentAnimator;

    private Coroutine talkingCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }


    private void Update()
    {

        UpdateAnimatorSpeed();
    }

    /// <summary>
    /// Assigns the animator that should be controlled by the stress level.
    /// </summary>
    public void SetCurrentAnimator(Animator animator)
    {
        currentAnimator = animator;
    }

    private void UpdateAnimatorSpeed()
    {
        if (currentAnimator != null)
        {
            currentAnimator.speed = GetCalculatedSpeed();
        }
    }



    /// <summary>
    /// Returns the calculated speed based on the current stress level.
    /// </summary>
    public float GetCalculatedSpeed()
    {
        return baseSpeed + (stressLevel * (maxStressSpeedMultiplier - baseSpeed));
    }

    /// <summary>
    /// Sets the "talking" boolean on the current animator for a specific duration.
    /// </summary>
    public void SetTalkingState(bool isTalking, float duration = 0f)
    {
        if (currentAnimator == null) return;

        if (talkingCoroutine != null) StopCoroutine(talkingCoroutine);

        if (isTalking && duration > 0f)
        {
            talkingCoroutine = StartCoroutine(TalkingRoutine(duration));
        }
        else
        {
            currentAnimator.SetInteger("AnimationNr", Random.Range(0, 2));
            currentAnimator.SetBool("Talking", isTalking);
        }
    }

    private IEnumerator TalkingRoutine(float duration)
    {
        if (currentAnimator != null)
        {
            currentAnimator.SetInteger("AnimationNr", Random.Range(0, 2));
            currentAnimator.SetBool("Talking", true);
        }
        yield return new WaitForSeconds(duration);
        if (currentAnimator != null)
        {
            currentAnimator.SetInteger("AnimationNr", Random.Range(0, 2));
            currentAnimator.SetBool("Talking", false);
        }
        talkingCoroutine = null;
    }

    /// <summary>
    /// Triggers a specific one-shot animation via string name.
    /// Expected names: RubArm, Dissaproval, Disbelief, Fist
    /// </summary>
    public void TriggerBodyAnimation(string animationName)
    {
        if (currentAnimator != null && !string.IsNullOrEmpty(animationName))
        {
            currentAnimator.SetTrigger(animationName);
        }
    }
}