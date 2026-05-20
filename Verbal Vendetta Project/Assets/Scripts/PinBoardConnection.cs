using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PinBoardConnection : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public PinBoardItem itemA;
    public PinBoardItem itemB;
    
    [Header("Visual Settings")]
    public float threadThickness = 5f; // Change this value to make it thinner/thicker
    
    private RectTransform rectTransform;

    public void Initialize(PinBoardItem a, PinBoardItem b)
    {
        itemA = a;
        itemB = b;
        
        Image img = gameObject.AddComponent<Image>();
        img.color = new Color(0.8f, 0.1f, 0.1f, 0.8f); // Red thread
        img.raycastTarget = true; // So it can be clicked
        
        rectTransform = GetComponent<RectTransform>();
        rectTransform.pivot = new Vector2(0.5f, 0.5f); // Center pivot for rotation
        
        UpdateLine();
    }

    private void Update()
    {
        if (itemA == null || itemB == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLine();
    }

    private void UpdateLine()
    {
        if (rectTransform == null || itemA == null || itemB == null) return;

        RectTransform rectA = itemA.GetComponent<RectTransform>();
        RectTransform rectB = itemB.GetComponent<RectTransform>();
        
        if (rectA == null || rectB == null) return;

        Vector2 localA = rectA.anchoredPosition;
        Vector2 localB = rectB.anchoredPosition;
        Vector2 localDir = localB - localA;
        
        rectTransform.anchoredPosition = localA + localDir / 2;
        rectTransform.sizeDelta = new Vector2(localDir.magnitude, threadThickness); 
        float localAngle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
        rectTransform.localRotation = Quaternion.Euler(0, 0, localAngle);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TooltipManager.Hide();
            Destroy(gameObject);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipManager.Show("Right Click to Remove Thread");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Hide();
    }
}
