using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    public InventoryManager inventoryManager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("pickable"))
        {
            ItemValue itemValue = other.GetComponent<ItemValue>();

            if (itemValue != null && itemValue.itemValue != null)
            {
                bool wasAdded = inventoryManager.AddItem(itemValue.itemValue);

                if (wasAdded)
                {
                    Destroy(other.gameObject); // Only destroy if added successfully
                    Debug.Log($"Picked up {itemValue.itemValue.name}");
                }
                else
                {
                    Debug.Log("Inventory full. Could not pick up " + itemValue.itemValue.name);
                }
            }
            else
            {
                Debug.LogWarning("ItemValue component missing or itemValue is null on " + other.name);
            }
        }
    }
}
