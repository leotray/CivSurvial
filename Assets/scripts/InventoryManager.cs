using UnityEngine;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public inventorySlot[] inventorySlots;
    public GameObject inventoryItemPrefab;

    public Transform handTransform;
    public Transform leftHandTransform;
    public Transform normalLeftHandPosition;    // Assign in Inspector
    public Transform blockingLeftHandPosition;  // Assign in Inspector

    public inventorySlot shieldSlot;

    public GameObject currentHeldItem;      // Right-hand item
    public GameObject currentLeftHeldItem;  // Left-hand (shield) item
    public int selectedSlot = -1;

    private void Start()
    {
        ChangeSelectedSlot(0);
    }

    private void Update()
    {
        if (Input.inputString != null)
        {
            bool isNumber = int.TryParse(Input.inputString, out int Number);
            if (isNumber && Number > 0 && Number < 10)
            {
                ChangeSelectedSlot(Number - 1);
            }
        }
    }

    void ChangeSelectedSlot(int newValue)
    {
        if (selectedSlot >= 0)
            inventorySlots[selectedSlot].DeSelected();

        inventorySlots[newValue].Select();
        selectedSlot = newValue;
        UpdateHeldItem();
    }

    public bool HasItem(Item item, int requiredAmount)
    {
        int totalCount = 0;
        foreach (inventorySlot slot in inventorySlots)
        {
            inventoryItem itemInSlot = slot.GetComponentInChildren<inventoryItem>();
            if (itemInSlot != null && itemInSlot.item == item)
            {
                totalCount += itemInSlot.count;
                if (totalCount >= requiredAmount)
                    return true;
            }
        }
        return false;
    }

    public bool RemoveItem(Item item, int amountToRemove)
    {
        if (!HasItem(item, amountToRemove))
            return false;

        int remainingToRemove = amountToRemove;

        foreach (inventorySlot slot in inventorySlots)
        {
            if (remainingToRemove <= 0) break;

            inventoryItem itemInSlot = slot.GetComponentInChildren<inventoryItem>();
            if (itemInSlot != null && itemInSlot.item == item)
            {
                int amountAvailable = itemInSlot.count;
                int amountDeducted = Mathf.Min(amountAvailable, remainingToRemove);

                itemInSlot.count -= amountDeducted;
                remainingToRemove -= amountDeducted;

                itemInSlot.RefreshCount();

                if (itemInSlot.count <= 0)
                    Destroy(itemInSlot.gameObject);
            }
        }

        UpdateHeldItem();
        return true;
    }

    public bool AddItem(Item item)
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlot invSlot = inventorySlots[i];
            inventoryItem itemInSlot = invSlot.GetComponentInChildren<inventoryItem>();
            if (itemInSlot != null && itemInSlot.item == item && itemInSlot.count < item.itemMaxCount)
            {
                itemInSlot.count++;
                itemInSlot.RefreshCount();
                return true;
            }
        }

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlot invSlot = inventorySlots[i];
            inventoryItem itemInSlot = invSlot.GetComponentInChildren<inventoryItem>();
            if (itemInSlot == null)
            {
                SpawnNewItem(item, invSlot);
                return true;
            }
        }
        return false;
    }

    public void SpawnNewItem(Item item, inventorySlot slot)
    {
        GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
        inventoryItem invItem = newItemGO.GetComponent<inventoryItem>();
        invItem.InitializeItem(item);
    }

    public void UpdateHeldItem()
    {
        // Destroy previous held items
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }
        if (currentLeftHeldItem != null)
        {
            Destroy(currentLeftHeldItem);
            currentLeftHeldItem = null;
        }

        // Right-hand (hotbar)
        if (selectedSlot >= 0)
        {
            inventoryItem selectedItem = inventorySlots[selectedSlot].GetComponentInChildren<inventoryItem>();
            if (selectedItem != null && selectedItem.item != null && selectedItem.item.itemPrefab != null)
            {
                currentHeldItem = Instantiate(
                    selectedItem.item.itemPrefab,
                    handTransform.position,
                    handTransform.rotation,
                    handTransform
                );

                Animator animator = currentHeldItem.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool("isPickedUp", true);
                    StartCoroutine(StopAnimation(animator, currentHeldItem));
                }
            }
        }

        // Left-hand (shield slot)
        inventoryItem shieldItem = shieldSlot.GetComponentInChildren<inventoryItem>();
        if (shieldItem != null && shieldItem.item != null && shieldItem.item.isShield && shieldItem.item.itemPrefab != null)
        {
            currentLeftHeldItem = Instantiate(
                shieldItem.item.itemPrefab,
                leftHandTransform.position,
                leftHandTransform.rotation,
                leftHandTransform
            );
        }
    }

    private IEnumerator StopAnimation(Animator animator, GameObject sourceObject)
    {
        yield return new WaitForSeconds(0.3f);

        if ((currentHeldItem == sourceObject || currentLeftHeldItem == sourceObject) && animator != null)
        {
            animator.SetBool("isPickedUp", false);
        }
    }

    public void SetShieldBlockingPose(bool isBlocking)
    {
        if (leftHandTransform != null && normalLeftHandPosition != null && blockingLeftHandPosition != null)
        {
            Transform target = isBlocking ? blockingLeftHandPosition : normalLeftHandPosition;

            leftHandTransform.localPosition = target.localPosition;
            leftHandTransform.localRotation = target.localRotation;
        }
    }
}
