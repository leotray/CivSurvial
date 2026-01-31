using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class inventorySlot : MonoBehaviour, IDropHandler
{
    public Image image;
    public Color selected, notselected;
    public bool isShieldSlot = false; // ✅ Set in Inspector for shield slot

    private void Awake()
    {
        DeSelected();
    }

    public void Select()
    {
        image.color = selected;
    }

    public void DeSelected()
    {
        image.color = notselected;
    }

    public void OnDrop(PointerEventData eventData)
    {
        var draggedItem = eventData.pointerDrag?.GetComponent<inventoryItem>();
        if (draggedItem == null) return;

        inventoryItem itemInSlot = GetComponentInChildren<inventoryItem>();

        // Optional: Restrict shield slot to shield items
        if (isShieldSlot && !draggedItem.item.isShield)
        {
            Debug.Log("❌ Only shield items can go in the shield slot.");
            return;
        }

        // CASE 1: Empty slot → move item
        if (itemInSlot == null)
        {
            draggedItem.transform.SetParent(transform);
            draggedItem.transform.localPosition = Vector3.zero;

            // Clear old parent if it was a special UI (e.g. furnace)
            var oldSlot = draggedItem.parentBeforeDrag?.GetComponent<FurnaceSlotUI>();
            if (oldSlot != null)
                oldSlot.ClearSlot();

            return;
        }

        // CASE 2: Same item → stack
        if (itemInSlot.item == draggedItem.item && itemInSlot.count < itemInSlot.item.itemMaxCount)
        {
            int spaceLeft = itemInSlot.item.itemMaxCount - itemInSlot.count;
            int transferAmount = Mathf.Min(spaceLeft, draggedItem.count);

            itemInSlot.count += transferAmount;
            itemInSlot.RefreshCount();

            draggedItem.count -= transferAmount;
            if (draggedItem.count <= 0)
            {
                Destroy(draggedItem.gameObject);
            }
            else
            {
                draggedItem.RefreshCount();
            }

            var oldSlot = draggedItem.parentBeforeDrag?.GetComponent<FurnaceSlotUI>();
            if (oldSlot != null)
                oldSlot.ClearSlot();

            return;
        }

        // CASE 3: Cannot stack → swap
        if (itemInSlot != null && draggedItem != null)
        {
            Transform oldParent = draggedItem.parentBeforeDrag;
            draggedItem.transform.SetParent(transform);
            draggedItem.transform.localPosition = Vector3.zero;

            itemInSlot.transform.SetParent(oldParent);
            itemInSlot.transform.localPosition = Vector3.zero;
        }
    }
}
