using UnityEngine;
using UnityEngine.EventSystems;

public class ScrapUI : PinBoardItem, IPointerEnterHandler, IPointerExitHandler
{
    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TooltipManager.Hide();
            PinBoardItem.CancelConnection();
            Destroy(gameObject);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipManager.Show("Left Click & Drag to Move\nClick to start/end Thread\nRight Click to Remove");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Hide();
    }
}
