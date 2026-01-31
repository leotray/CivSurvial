using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI slot for multiplayer inventory. Restricts optional shield slot,
/// updates icon/count, and requests swap/merge on drop.
/// </summary>
public class MPInventorySlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    public Image icon;
    public Text countText;
    public Image background;
    public Color selectedColor = Color.white;
    public Color normalColor = Color.gray;

    [Header("Links")]
    public MPInventoryManager owner;
    public int index;

    [Header("Rules")]
    public bool isShieldSlot = false; // Optional: restrict to shield items

    public void Select() => background.color = selectedColor;
    public void DeSelect() => background.color = normalColor;

    public void Refresh(InventoryItemData data, int i)
    {
        index = i;

        // ❌ Removed "data == null" (structs can’t be null)
        if (string.IsNullOrEmpty(data.itemGuid) || data.count <= 0)
        {
            Clear();
            return;
        }

        var item = GameData.LookupItem(data.itemGuid);
        if (item != null)
        {
            if (icon != null)
            {
                icon.sprite = item.icon;
                icon.enabled = true;
            }
            if (countText != null)
                countText.text = data.count > 1 ? "x" + data.count.ToString() : "";
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }
        if (countText != null)
            countText.text = "";
    }

    public void OnPointerEnter(PointerEventData eventData) { }
    public void OnPointerExit(PointerEventData eventData) { }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag?.GetComponent<MPInventoryItem>();
        if (dragged == null) return;
        if (owner == null) return;

        int fromIndex = dragged.homeSlotIndex;
        int toIndex = index;

        // Optional: shield slot restriction
        if (isShieldSlot)
        {
            if (fromIndex >= 0 && fromIndex < owner.inventory.Count)
            {
                var data = owner.inventory[fromIndex];
                if (!string.IsNullOrEmpty(data.itemGuid))
                {
                    var def = GameData.LookupItem(data.itemGuid);
                    if (def != null && !def.isShield)
                    {
                        return; // reject drop
                    }
                }
            }
        }

        // Reparent visual instantly for snappy UX
        dragged.transform.SetParent(this.transform, false);
        if (dragged.rectTransform != null)
            dragged.rectTransform.anchoredPosition = Vector2.zero;

        // Update local visual's home index
        dragged.homeSlotIndex = toIndex;

        // Ask server to do the actual swap/merge
        owner.RequestSwapOrMerge(fromIndex, toIndex);
    }
}
