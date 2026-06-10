using UnityEngine;
using TMPro;

public class TranscriptCardInteractable : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text transcriptTextDisplay;
    
    [Header("Settings")]
    public float lerpSpeed = 10f;
    public float hoverDelay = 0.5f;
    public float unhoverDelay = 0.5f;
    
    private string fullTranscriptText;
    private Transform currentSlot;
    private Transform hoverLocation;
    
    private bool isHovered = false;
    private bool isPressed = false;
      private float hoverTimer = 0f;
    private float unhoverTimer = 0f;

    public void Setup(string text, Transform assignedSlot, Transform hoverPos)
    {
        fullTranscriptText = text;
        if (transcriptTextDisplay != null)
        {
            transcriptTextDisplay.text = text;
        }
        
        currentSlot = assignedSlot;
        hoverLocation = hoverPos;

        // Snap to initial position immediately
        transform.position = currentSlot.position;
        transform.rotation = currentSlot.rotation;
    }

    public void UpdateTargetSlot(Transform newSlot)
    {
        currentSlot = newSlot;
    }

    void Update()
    {
        HandleRaycastInteraction();
        HandleMovementLerp();
    }

    private void HandleRaycastInteraction()
    {
        if (GameManager.Instance == null) return;
        
        var state = GameManager.Instance.currentState;
        if (state != GameManager.GameState.Interrogation)
        {
            if (isHovered) SetHover(false);
            return;
        }

        // Block input if Pin Board is open
        if (PinBoardManager.Instance != null && PinBoardManager.Instance.IsOpen)
        {
            if (isHovered) SetHover(false);
            return;
        }

        Camera cam = GameManager.Instance.mainCamera != null ? GameManager.Instance.mainCamera : Camera.main;
        if (cam == null) return;

        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        }
        else
        {
            ray = cam.ScreenPointToRay(Input.mousePosition);
        }

        bool isCurrentlyRaycasting = false;
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                isCurrentlyRaycasting = true;

                if (!isHovered && !isPressed)
                {
                    hoverTimer += Time.deltaTime;
                    if (hoverTimer >= hoverDelay)
                    {
                        SetHover(true);
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {
                    PressCard();
                }
            }
        }

        if (!isCurrentlyRaycasting)
        {
            if (!isHovered)
            {
                hoverTimer = 0f;  // Reset hover timer when we leave
            }
            
            if (isHovered && !isPressed) 
            {
                unhoverTimer += Time.deltaTime;
                if (unhoverTimer >= unhoverDelay)
                {
                    SetHover(false);
                }
            }
        }

        if (isPressed && Input.GetMouseButtonUp(0))
        {
            ReleaseCard();
        }
    }

    private void SetHover(bool hovered)
    {
        isHovered = hovered;
        if (hovered)
        {
            unhoverTimer = 0f;
            TooltipManager.Show("Left Click to Pin to Board");
        }
        else
        {
            TooltipManager.Hide();
        }
    }

    private void HandleMovementLerp()
    {
        if (currentSlot == null) return;

        Transform targetTransform = (isHovered && hoverLocation != null) ? hoverLocation : currentSlot;

        transform.position = Vector3.Lerp(transform.position, targetTransform.position, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetTransform.rotation, Time.deltaTime * lerpSpeed);
    }

    private void PressCard()
    {
        if (isPressed) return;
        isPressed = true;
    }

    private void ReleaseCard()
    {
        if (!isPressed) return;
        isPressed = false;

        // Ensure we were still hovering when we released
        if (isHovered)
        {
            PinToBoard();
        }
    }

    private void PinToBoard()
    {
        if (PinBoardManager.Instance != null)
        {
            PinBoardManager.Instance.AddEvidenceScrap(fullTranscriptText);
            
            // Unlink from manager so it stops managing it
            TranscriptCardManager manager = FindObjectOfType<TranscriptCardManager>();
            if (manager != null)
            {
                manager.RemoveCard(this);
            }

            TooltipManager.Hide();

            // Destroy physical card on desk
            Destroy(gameObject);
        }
    }
}
