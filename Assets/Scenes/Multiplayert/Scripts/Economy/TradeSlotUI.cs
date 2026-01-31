using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TradeSlotUI : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Text countText;

    public InventoryItemData? itemData;

    private void Awake()
    {
        Clear();
    }

    public void Set(InventoryItemData data)
    {
        itemData = data;
        icon.enabled = true;
        icon.sprite = GameData.LookupItem(data.itemGuid).icon;
        countText.text = data.count > 1 ? data.count.ToString() : "";
    }

    public void Clear()
    {
        itemData = null;
        icon.enabled = false;
        countText.text = "";
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = DragDropHandler.CurrentDraggedItem;
        if (!dragged.HasValue)
        {
            Debug.Log("[TradeSlotUI] OnDrop: no dragged item in DragDropHandler.");
            return;
        }

        var draggedValue = dragged.Value;

        // Only allow dropping from your inventory into YOUR trade panel (UI should prevent dropping into partner's slots)
        if (!itemData.HasValue)
        {
            int stackLimit = GameData.LookupItem(draggedValue.itemGuid).itemMaxCount;
            int placeCount = Mathf.Min(draggedValue.count, stackLimit);

            Set(new InventoryItemData { itemGuid = draggedValue.itemGuid, count = placeCount });
            draggedValue.count -= placeCount;

            if (draggedValue.count <= 0)
                DragDropHandler.ClearDragged();
            else
                DragDropHandler.StartDrag(draggedValue);
        }
        else
        {
            var current = itemData.Value;

            // Merge if same item type
            if (current.itemGuid == draggedValue.itemGuid)
            {
                int stackLimit = GameData.LookupItem(draggedValue.itemGuid).itemMaxCount;
                int space = stackLimit - current.count;

                int toAdd = Mathf.Min(space, draggedValue.count);
                current.count += toAdd;
                Set(current);

                draggedValue.count -= toAdd;

                if (draggedValue.count <= 0)
                    DragDropHandler.ClearDragged();
                else
                    DragDropHandler.StartDrag(draggedValue);
            }
        }

        Debug.Log("[TradeSlotUI] OnDrop: updated slot; refreshing trade offer UI.");
        var tradeUI = FindObjectOfType<TradeUIManager>();
        if (tradeUI != null)
        {
            tradeUI.RefreshMyOffer();
        }
    }
}
