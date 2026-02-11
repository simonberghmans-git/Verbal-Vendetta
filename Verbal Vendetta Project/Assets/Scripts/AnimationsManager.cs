using UnityEngine;

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

    [Header("Global Idle Variety")]
    [Tooltip("The current idle variation index that all suspects should follow.")]
    public int currentIdleIndex = 0;
    public float minIdleTime = 4f;
    public float maxIdleTime = 8f;
    
    private float nextIdleSwitchTime;

    // Reference to the currently active suspect's Animator
    private Animator currentAnimator;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        HandleIdleTimer();
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
    /// Calculates when to switch the idle variation for the room.
    /// </summary>
    private void HandleIdleTimer()
    {
        if (Time.time >= nextIdleSwitchTime)
        {
            // Pick a new random idle index (0-2)
            currentIdleIndex = Random.Range(0, 3);
            
            // Set the next random interval
            nextIdleSwitchTime = Time.time + Random.Range(minIdleTime, maxIdleTime);
        }
    }

    /// <summary>
    /// Returns the calculated speed based on the current stress level.
    /// </summary>
    public float GetCalculatedSpeed()
    {
        return baseSpeed + (stressLevel * (maxStressSpeedMultiplier - baseSpeed));
    }
}