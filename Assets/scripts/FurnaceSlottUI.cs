using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FurnaceSlotUI : MonoBehaviour, IDropHandler
{
    public Image icon;
    public Item currentItem;

    public enum SlotType { Input, Fuel, Output }
    public SlotType slotType;

    public void SetItem(Item item)
    {
        currentItem = item;
        icon.sprite = item != null ? item.sprite : null;
        icon.enabled = item != null;
    }
    public void ConsumeOne()
    {
        inventoryItem itemInSlot = GetComponentInChildren<inventoryItem>();
        if (itemInSlot != null)
        {
            itemInSlot.count--;
            itemInSlot.RefreshCount();

            if (itemInSlot.count <= 0)
            {
                Destroy(itemInSlot.gameObject);
                ClearSlot(); // resets icon + currentItem
            }
        }
    }

    public void ClearSlot()
    {
        SetItem(null);
    }

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Drop triggered on: " + gameObject.name);

        if (slotType == SlotType.Output)
        {
            Debug.Log("Cannot drop into output slot.");
            return;
        }

        var draggedItem = eventData.pointerDrag?.GetComponent<inventoryItem>();
        if (draggedItem == null)
        {
            Debug.Log("No dragged item found.");
            return;
        }

        // Prevent dropping if slot is already occupied
        if (currentItem != null)
        {
            Debug.Log("Slot already has an item.");
            return;
        }

        // Only allow fuel in fuel slot
        if (slotType == SlotType.Fuel && !draggedItem.item.isFuel)
        {
            Debug.Log("This item is not valid fuel.");
            return;
        }

        // ✅ Reparent visually
        draggedItem.transform.SetParent(transform);
        draggedItem.transform.localPosition = Vector3.zero;

        // ✅ Determine source and handle updates
        var cameFromInventory = draggedItem.parentBeforeDrag.GetComponent<inventorySlot>() != null;
        var cameFromFurnaceSlot = draggedItem.parentBeforeDrag.GetComponent<FurnaceSlotUI>();

        if (cameFromFurnaceSlot != null)
        {
            cameFromFurnaceSlot.ClearSlot();
            Debug.Log("Cleared previous furnace slot.");
        }

        // ✅ Set this slot's current item (used for smelting)
        SetItem(draggedItem.item);

        // ✅ Do NOT subtract count — just move the item
        // (Remove this section to preserve stack integrity)
        // if (draggedItem.count <= 1)
        // {
        //     Destroy(draggedItem.gameObject);
        // }
        // else
        // {
        //     draggedItem.count--;
        //     draggedItem.RefreshCount();
        // }
    }


}



