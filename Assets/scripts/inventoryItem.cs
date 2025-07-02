using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class inventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image image;
    public Text countText;

    [HideInInspector] public Transform parentBeforeDrag;
    [HideInInspector] public Item item;
    [HideInInspector] public int count = 1;

    public void InitializeItem(Item newItem)
    {
        item = newItem;
        image.sprite = newItem.sprite;
        RefreshCount();
    }

    public void RefreshCount()
    {
        countText.text = count.ToString();
        countText.gameObject.SetActive(count > 1);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        image.raycastTarget = false;
        parentBeforeDrag = transform.parent;
        transform.SetParent(transform.root); // move to top level (UI hierarchy)
        transform.SetAsLastSibling(); // ensures it appears on top visually
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (this == null || image == null) return; // ← Prevents destroyed references

        image.raycastTarget = true;

        if (transform.parent != transform.root && transform.parent != parentBeforeDrag)
        {
            transform.localPosition = Vector3.zero;
        }
        else
        {
            transform.SetParent(parentBeforeDrag);
            transform.localPosition = Vector3.zero;
        }
    }

}
