using UnityEngine;
using UnityEngine.EventSystems;

public class PinBoardItem : MonoBehaviour, IPointerClickHandler
{
    private static PinBoardItem selectedItem;

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 1)
            {
                HandleConnectionSelection();
            }
        }
    }

    protected virtual void Update()
    {
        // Cancel connection state if this is the currently selected item and player right-clicks
        if (selectedItem == this && Input.GetMouseButtonDown(1))
        {
            CancelConnection();
        }
    }

    private void HandleConnectionSelection()
    {
        if (selectedItem == null)
        {
            // Start connection silently
            selectedItem = this;
        }
        else if (selectedItem == this)
        {
            // Cancel connection
            CancelConnection();
        }
        else
        {
            // Complete connection
            CreateConnection(selectedItem, this);
            CancelConnection(); // Clears selectedItem
        }
    }

    public static void CancelConnection()
    {
        selectedItem = null;
    }

    private void CreateConnection(PinBoardItem itemA, PinBoardItem itemB)
    {
        GameObject lineObj = new GameObject("PinBoardThread");
        lineObj.transform.SetParent(itemA.transform.parent, false);
        lineObj.transform.SetAsFirstSibling(); 

        PinBoardConnection connection = lineObj.AddComponent<PinBoardConnection>();
        connection.Initialize(itemA, itemB);
    }
}
