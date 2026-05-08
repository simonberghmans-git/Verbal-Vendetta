using UnityEngine;

/// <summary>
/// Attached to a physical object (Phone, Handcuffs) on the interrogation table.
/// Clicking this object triggers the Accusation Phase.
/// </summary>
public class AccusationTrigger : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color highlightColor = new Color(1, 0.8f, 0, 1); // Gold/Yellow for accusation
    public float highlightIntensity = 1.0f;

    private SuspectHighlight highlight;
    private bool isHovered = false;

    void Start()
    {
        // Try to get or add SuspectHighlight for consistent visual feedback
        highlight = GetComponent<SuspectHighlight>();
        if (highlight == null)
        {
            highlight = gameObject.AddComponent<SuspectHighlight>();
            highlight.materialColor = highlightColor;
            highlight.highlightIntensity = highlightIntensity;
        }
    }

    void Update()
    {
        // Only allow interaction in SubjectSelection or Interrogation states
        if (GameManager.Instance == null) return;
        
        var state = GameManager.Instance.currentState;
        if (state != GameManager.GameState.SubjectSelection && state != GameManager.GameState.Interrogation)
        {
            if (isHovered) SetHover(false);
            return;
        }

        // Raycast from camera to detect hover and click
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // If cursor is locked (Interrogation), use center of screen
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        }

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (!isHovered) SetHover(true);

                if (Input.GetMouseButtonDown(0))
                {
                    TriggerAccusation();
                }
            }
            else
            {
                if (isHovered) SetHover(false);
            }
        }
        else
        {
            if (isHovered) SetHover(false);
        }
    }

    private void SetHover(bool hovered)
    {
        isHovered = hovered;
        if (highlight != null)
        {
            highlight.SetSelected(hovered);
        }
    }

    private void TriggerAccusation()
    {
        Debug.Log("[AccusationTrigger] Item clicked! Starting Accusation Phase.");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartAccusationPhase();
        }
        
        // Play pick up sound if InterrogationManager has one
        var intMan = FindObjectOfType<InterrogationManager>();
        if (intMan != null && intMan.accusationTriggerClip != null)
        {
            AudioSource source = intMan.GetComponent<AudioSource>();
            if (source != null)
            {
                source.PlayOneShot(intMan.accusationTriggerClip);
            }
        }
    }
}
