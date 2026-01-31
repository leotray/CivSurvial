using UnityEngine;

public static class DragDropHandler
{
    public static InventoryItemData? CurrentDraggedItem { get; private set; }

    public static void StartDrag(InventoryItemData data)
    {
        CurrentDraggedItem = data;
        Debug.Log($"[DragDropHandler] StartDrag {data.itemGuid} x{data.count}");
    }

    public static void ClearDragged()
    {
        Debug.Log("[DragDropHandler] ClearDragged");
        CurrentDraggedItem = null;
    }
}
