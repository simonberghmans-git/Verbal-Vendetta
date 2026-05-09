using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles dragging UI elements on a World Space Canvas.
/// </summary>
public class DraggableEvidence : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    
    [Header("Aesthetics")]
    public float dragScale = 1.05f;
    private Vector3 originalScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        originalScale = transform.localScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        // Bring to front of its container
        transform.SetAsLastSibling();
        
        // Visual feedback
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        transform.localScale = originalScale * dragScale;
        
        // Optional: Trigger a "pickup" sound via a SoundManager if available
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;

        // Calculate new position
        Vector2 newPos = rectTransform.anchoredPosition + (eventData.delta / canvas.scaleFactor);

        // Confine to parent (Board) bounds
        if (transform.parent is RectTransform parentRect)
        {
            Vector2 sizeDelta = parentRect.rect.size;
            float halfWidth = sizeDelta.x / 2;
            float halfHeight = sizeDelta.y / 2;

            // Simple clamping logic - keeps the center of the item inside the board
            // You can refine this by subtracting half of the item's own width/height if you want
            newPos.x = Mathf.Clamp(newPos.x, -halfWidth, halfWidth);
            newPos.y = Mathf.Clamp(newPos.y, -halfHeight, halfHeight);
        }

        rectTransform.anchoredPosition = newPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = originalScale;
        
        // Optional: Trigger a "pin" sound
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Destroy(gameObject);
        }
    }
}
