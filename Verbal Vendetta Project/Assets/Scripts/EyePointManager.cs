using UnityEngine;

public class EyePointManager : MonoBehaviour
{
    public enum EyeState
    {
        Waiting,
        Thinking,
        Talking
    }

    public EyeState currentState = EyeState.Waiting;
    private float wanderingTimer;
    public Transform cameraPoint;
    public Transform notebook;
    private double glanceChance = 0;
    
    // New flag to force direct eye contact (for easy questions)
    public bool forceDirectEyeContact = false;

    public static EyePointManager Instance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case EyeState.Waiting:
                HandleWaiting();
                break;

            case EyeState.Thinking:
                HandleThinking();
                break;
            
            case EyeState.Talking:
                HandleTalking();
                break;
        }
    }

    private void HandleWaiting()
    {
        wanderingTimer -= Time.deltaTime;
        if (wanderingTimer <= 0)
        {
            // 20% chance to look at notebook (approx 1 in 5)
            // Random.Range(0, 5) returns 0, 1, 2, 3, 4. So 5 is never returned.
            // Changed logic: if 0, look at notebook.
            if (glanceChance != 0) 
            {
                // Look at camera (Default)
                transform.position = cameraPoint.position;
                // variable duration for looking at camera
                wanderingTimer = Random.Range(2.0f, 5.0f);
            }
            else
            {
                // Look at notebook
                if (notebook != null)
                {
                    transform.position = notebook.position;
                }
                // shorter duration for glancing at notebook
                wanderingTimer = Random.Range(1.0f, 2.5f);
            }
            glanceChance = Random.Range(0, 5);
        }
    }

   

    private Vector3 floorPoint;

    private void Start()
    {
        RandomizeFloorPoint();
    }

    public void RandomizeFloorPoint()
    {
        // Define a random floor point each time the script starts/suspect loads
        float randomX = Random.Range(-1f, 1f);
        float randomY = Random.Range(-1f, -0.1f);
        float randomZ = Random.Range(-1f, 1f);
        if (cameraPoint != null)
        {
            floorPoint = cameraPoint.position + new Vector3(randomX, randomY, randomZ);
        }
    }

    private void HandleThinking()
    {
        // Park eyes on the floor point
        transform.position = floorPoint;
    }

    private void HandleTalking()
    {
        wanderingTimer -= Time.deltaTime;
        if (wanderingTimer <= 0)
        {
            // If forced direct eye contact is enabled (easy question), prioritize camera
            if (forceDirectEyeContact)
            {
                 transform.position = cameraPoint.position;
                 wanderingTimer = Random.Range(1.5f, 3.0f); // Maintain contact
                 return;
            }

            // 20% chance to look at camera (approx 1 in 5)
            if (glanceChance == 0) // Changed logic: if chance hits specific number (e.g. 0), look at camera
            {
                transform.position = cameraPoint.position;
                
                // Hold gaze for a bit (look up mid-sentence)
                wanderingTimer = Random.Range(1f, 2.5f);
            }
            else
            {
                // Look back down at the floor point to "remember"
                transform.position = floorPoint;

                // Stay looking down for a bit
                wanderingTimer = Random.Range(2f, 4f);
            }
            
            glanceChance = Random.Range(0, 4);
        }
    }
}
