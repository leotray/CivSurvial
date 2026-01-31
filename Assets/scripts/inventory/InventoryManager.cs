using UnityEngine;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public inventorySlot[] inventorySlots;
    public GameObject inventoryItemPrefab;

    public Transform handTransform;
    public Transform leftHandTransform;
    public Transform normalLeftHandPosition;
    public Transform blockingLeftHandPosition;

    public inventorySlot shieldSlot;

    public GameObject currentHeldItem;
    public GameObject currentLeftHeldItem;
    public int selectedSlot = -1;

    public PlayerStats playerStats;

    private HorseMounting horseMounting;

    private void Start()
    {
        ChangeSelectedSlot(0);
        horseMounting = GetComponent<HorseMounting>();
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

        if (Input.GetMouseButtonDown(1))
        {
            inventoryItem selectedItem = selectedSlot >= 0 ? inventorySlots[selectedSlot].GetComponentInChildren<inventoryItem>() : null;

            if (selectedItem == null || selectedItem.item == null)
            {
                Debug.Log("🔍 No item selected to use.");
                return;
            }

            if (selectedItem.item.itemCategory == Item.ItemCategory.Food)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 5f))
                {
                    MountableHorse horse = hit.collider.GetComponentInParent<MountableHorse>();
                    if (horse != null)
                    {
                        Debug.Log("🎯 Looking at a horse. Trying to tame...");
                        if (selectedItem.item.tamesHorses)
                        {
                            Debug.Log("✅ This food CAN tame horses.");
                            if (!horse.isTamed)
                            {
                                Debug.Log("🐴 Horse is NOT tamed. Taming now...");
                                horse.isTamed = true;
                                RemoveItem(selectedItem.item, 1);
                                Debug.Log("🎉 Horse has been tamed!");
                                return;
                            }
                            else
                            {
                                Debug.Log("ℹ️ Horse is already tamed.");
                                return;
                            }
                        }
                        else
                        {
                            Debug.Log("❌ This food cannot tame horses.");
                        }
                    }
                }

                // Regular eating (only if not taming a horse)
                if (playerStats.currentHunger >= playerStats.maxHunger)
                {
                    Debug.Log("⚠️ Hunger is already full.");
                    return;
                }

                Debug.Log("🍖 Eating food...");
                playerStats.ModifyHunger(selectedItem.item.hungerRestore);
                playerStats.TryHealFromFood(selectedItem.item.hungerRestore);
                RemoveItem(selectedItem.item, 1);
            }
        }

        if (Input.GetMouseButtonDown(1)) SetShieldBlockingPose(true);
        if (Input.GetMouseButtonUp(1)) SetShieldBlockingPose(false);
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

        if (playerStats != null)
        {
            playerStats.isBlocking = isBlocking;
        }
    }
}
