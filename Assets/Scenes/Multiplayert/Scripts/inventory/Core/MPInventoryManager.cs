using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Server-authoritative multiplayer inventory with fixed-size slots.
/// Empty slots are stored as { itemGuid:"", count:0 } to keep indices stable.
/// </summary>
public class MPInventoryManager : NetworkBehaviour
{
    [Header("UI")]
    public GameObject inventoryUI;
    public MPInventorySlot[] slotsUI;
    public int selectedSlot = 0;
    public Transform handTransform;
    public GameObject currentHeldItem;

    [Header("Config")]
    [Tooltip("Total number of inventory slots. Should match slotsUI.Length.")]
    public int maxSlots = 9;

    public readonly SyncList<InventoryItemData> inventory = new SyncList<InventoryItemData>();

    private const int itemMaxStackDefault = 99;
    private string heldItemGuid = "";

    #region Unity & FishNet lifecycle
    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureFixedSize();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (inventoryUI != null)
            inventoryUI.SetActive(IsOwner);

        // ✅ only subscribe if this is the owner client
        if (IsOwner)
            inventory.OnChange += HandleInventoryChanged;

        RefreshAllSlots();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (IsOwner)
            inventory.OnChange -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged(SyncListOperation op, int index, InventoryItemData oldItem, InventoryItemData newItem, bool asServer)
    {
        RefreshAllSlots();
    }

    private void Update()
    {
        if (!IsOwner) return;

        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                ChangeSelectedSlot(i);
        }
    }
    #endregion

    #region Selection & held item
    public void ChangeSelectedSlot(int newIndex)
    {
        if (slotsUI == null || slotsUI.Length == 0) return;

        newIndex = Mathf.Clamp(newIndex, 0, slotsUI.Length - 1);

        if (selectedSlot >= 0 && selectedSlot < slotsUI.Length && slotsUI[selectedSlot] != null)
            slotsUI[selectedSlot].DeSelect();

        selectedSlot = newIndex;

        if (selectedSlot >= 0 && selectedSlot < slotsUI.Length && slotsUI[selectedSlot] != null)
            slotsUI[selectedSlot].Select();

        string guid = "";
        if (selectedSlot >= 0 && selectedSlot < inventory.Count)
            guid = inventory[selectedSlot].itemGuid;

        SetHeldItemServerRpc(guid);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetHeldItemServerRpc(string guid)
    {
        heldItemGuid = guid ?? "";
        UpdateHeldItemObserversRpc(heldItemGuid);
    }

    [ObserversRpc(BufferLast = true, ExcludeOwner = false)]
    private void UpdateHeldItemObserversRpc(string guid)
    {
        ApplyHeldItem(guid);
    }

    private void ApplyHeldItem(string guid)
    {
        heldItemGuid = guid ?? "";

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        if (string.IsNullOrEmpty(heldItemGuid)) return;

        ItemData item = GameData.LookupItem(heldItemGuid);
        if (item != null && item.itemPrefab != null)
        {
            currentHeldItem = Instantiate(item.itemPrefab);

            PlayerMovementMultiplayer movement = GetComponent<PlayerMovementMultiplayer>();
            if (movement != null)
            {
                Transform holder = IsOwner ? movement.heldItemPlaceholder : movement.remoteItemHolder;

                currentHeldItem.transform.SetParent(holder);
                currentHeldItem.transform.localPosition = Vector3.zero;
                currentHeldItem.transform.localRotation = Quaternion.identity;
            }
        }
    }
    #endregion

    #region Refresh UI
    private void RefreshAllSlots()
    {
        if (!IsOwner) return; // ✅ only owner should refresh UI
        if (slotsUI == null) return;

        if (inventory.Count != maxSlots)
            EnsureFixedSize();

        for (int i = 0; i < slotsUI.Length; i++)
        {
            var slot = slotsUI[i];
            if (slot == null || slot.Equals(null)) continue; // ✅ skip destroyed

            if (i < inventory.Count)
                slot.Refresh(inventory[i], i);
            else
                slot.Clear();

            var visual = slot.GetComponentInChildren<MPInventoryItem>();
            if (visual != null)
                visual.homeSlotIndex = i;
        }
    }
    #endregion

    #region Server add/remove/swap/merge
    private static InventoryItemData EmptyItem => new InventoryItemData { itemGuid = "", count = 0 };

    private void EnsureFixedSize()
    {
        int desired = maxSlots > 0 ? maxSlots : (slotsUI != null && slotsUI.Length > 0 ? slotsUI.Length : inventory.Count);
        maxSlots = desired;

        if (inventory.Count < desired)
        {
            while (inventory.Count < desired)
                inventory.Add(EmptyItem);
        }
        else if (inventory.Count > desired)
        {
            for (int i = inventory.Count - 1; i >= desired; i--)
                inventory.RemoveAt(i);
        }
    }

    public void AddItemServer(string guid, int count = 1)
    {
        if (!IsServer) return;
        if (string.IsNullOrEmpty(guid) || count <= 0) return;

        EnsureFixedSize();

        ItemData data = GameData.LookupItem(guid);
        int maxStack = data != null ? data.itemMaxCount : itemMaxStackDefault;

        int remaining = count;

        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            var slot = inventory[i];
            if (!string.Equals(slot.itemGuid, guid, StringComparison.Ordinal)) continue;
            if (slot.count >= maxStack) continue;

            int space = maxStack - slot.count;
            int toAdd = Mathf.Min(space, remaining);
            slot.count += toAdd;
            inventory[i] = slot;

            remaining -= toAdd;
        }

        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            var slot = inventory[i];
            if (!string.IsNullOrEmpty(slot.itemGuid)) continue;

            int toAdd = Mathf.Min(maxStack, remaining);
            inventory[i] = new InventoryItemData { itemGuid = guid, count = toAdd };
            remaining -= toAdd;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestAddItem(string guid, int count = 1)
    {
        AddItemServer(guid, count);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSwapOrMerge(int fromIndex, int toIndex)
    {
        if (!IsServer) return;

        EnsureFixedSize();

        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= inventory.Count || toIndex >= inventory.Count) return;

        var a = inventory[fromIndex];
        var b = inventory[toIndex];

        if (string.IsNullOrEmpty(b.itemGuid))
        {
            inventory[toIndex] = a;
            inventory[fromIndex] = EmptyItem;
        }
        else if (!string.IsNullOrEmpty(a.itemGuid) && a.itemGuid == b.itemGuid)
        {
            ItemData data = GameData.LookupItem(a.itemGuid);
            int maxStack = data != null ? data.itemMaxCount : itemMaxStackDefault;

            int total = a.count + b.count;
            b.count = Mathf.Min(total, maxStack);
            a.count = total - b.count;

            inventory[toIndex] = b;
            inventory[fromIndex] = a.count > 0 ? a : EmptyItem;
        }
        else
        {
            inventory[toIndex] = a;
            inventory[fromIndex] = b;
        }
    }

    public bool HasItem(string guid, int amount)
    {
        if (!IsServer) return false;
        if (string.IsNullOrEmpty(guid) || amount <= 0) return false;

        int found = 0;
        for (int i = 0; i < inventory.Count; i++)
        {
            if (inventory[i].itemGuid == guid)
            {
                found += inventory[i].count;
                if (found >= amount) return true;
            }
        }
        return false;
    }

    public int RemoveItem(string guid, int amount)
    {
        if (!IsServer) return 0;
        if (string.IsNullOrEmpty(guid) || amount <= 0) return 0;

        int remaining = amount;
        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            if (inventory[i].itemGuid != guid) continue;

            var slot = inventory[i];
            int take = Mathf.Min(slot.count, remaining);
            slot.count -= take;
            remaining -= take;

            inventory[i] = (slot.count > 0) ? slot : EmptyItem;
        }

        return amount - remaining;
    }
    #endregion

    #region Client helpers
    /// <summary>
    /// Returns the selected item data if valid, otherwise null.
    /// </summary>
    public InventoryItemData? GetSelectedItemClient()
    {
        if (!IsOwner) return null;
        if (selectedSlot < 0 || selectedSlot >= inventory.Count) return null;

        var data = inventory[selectedSlot];
        if (string.IsNullOrEmpty(data.itemGuid) || data.count <= 0)
            return null;

        return data;
    }
    #endregion

    [Server]
    public void RemoveFromSlotServer(int slotIndex, int amount)
    {
        if (!IsServer) return;
        if (slotIndex < 0 || slotIndex >= inventory.Count) return;
        if (amount <= 0) return;

        var slot = inventory[slotIndex];
        if (string.IsNullOrEmpty(slot.itemGuid) || slot.count <= 0) return;

        int toRemove = Mathf.Min(slot.count, amount);
        slot.count -= toRemove;

        inventory[slotIndex] = (slot.count > 0)
            ? slot
            : new InventoryItemData { itemGuid = "", count = 0 };
    }
}
