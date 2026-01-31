using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Draggable visual for an inventory slot.
/// </summary>
public class MPInventoryItem : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [Tooltip("Index this item belongs to in the owner's inventory UI.")]
    public int homeSlotIndex;

    private CanvasGroup canvasGroup;
    public RectTransform rectTransform { get; private set; }
    private Transform originalParent;
    private Canvas parentCanvas;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;

        if (parentCanvas != null)
            transform.SetParent(parentCanvas.transform, false);

        // Optional: expose dragged data to other UIs
        var slot = originalParent != null ? originalParent.GetComponent<MPInventorySlot>() : null;
        if (slot != null && slot.owner != null && homeSlotIndex >= 0 && homeSlotIndex < slot.owner.inventory.Count)
        {
            InventoryItemData data = slot.owner.inventory[homeSlotIndex];
            DragDropHandler.StartDrag(data);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // If still on top-level canvas, it means the drop was rejected → snap back
        if (parentCanvas != null && (transform.parent == parentCanvas.transform || transform.parent == transform.root))
        {
            transform.SetParent(originalParent, false);
            rectTransform.anchoredPosition = Vector2.zero;

            DragDropHandler.ClearDragged();
            return;
        }

        // Dropped into a slot successfully; server will soon resync counts/icons
        DragDropHandler.ClearDragged();
    }
}
