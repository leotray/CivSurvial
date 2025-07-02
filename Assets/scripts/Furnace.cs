using System.Collections;
using UnityEngine;

public class Furnace : MonoBehaviour
{
    public FurnaceSlotUI inputSlot;
    public FurnaceSlotUI fuelSlot;
    public FurnaceSlotUI outputSlot;

    public FurnaceRecipe[] recipes;

    private bool isSmelting = false;

    void Update()
    {
        Debug.Log("Furnace Update running");

        if (!isSmelting && CanSmelt())
        {
            StartCoroutine(SmeltCoroutine());
        }
    }


    bool CanSmelt()
    {
        Debug.Log("Checking CanSmelt...");

        if (inputSlot.currentItem == null)
        {
            Debug.Log("Input is null");
            return false;
        }
        if (fuelSlot.currentItem == null)
        {
            Debug.Log("Fuel is null");
            return false;
        }

        if (!fuelSlot.currentItem.isFuel)
        {
            Debug.Log("Fuel item is not valid fuel");
            return false;
        }

        if (GetRecipe(inputSlot.currentItem) == null)
        {
            Debug.Log("No recipe for input item");
            return false;
        }

        return true;
    }



    FurnaceRecipe GetRecipe(Item input)
    {
        foreach (var recipe in recipes)
        {
            if (recipe.inputItem == input)
                return recipe;
        }
        return null;
    }

    IEnumerator SmeltCoroutine()
    {
        isSmelting = true;
        Debug.Log("started Smelting");

        FurnaceRecipe recipe = GetRecipe(inputSlot.currentItem);
        if (recipe == null)
        {
            isSmelting = false;
            yield break;
        }

        yield return new WaitForSeconds(recipe.smeltTime);

        // Stack or create output
        inventoryItem existingOutput = outputSlot.GetComponentInChildren<inventoryItem>();
        if (existingOutput != null &&
            existingOutput.item == recipe.outputItem &&
            existingOutput.count < recipe.outputItem.itemMaxCount)
        {
            existingOutput.count++;
            existingOutput.RefreshCount();
        }
        else if (existingOutput == null)
        {
            GameObject newItemGO = Instantiate(
                FindObjectOfType<InventoryManager>().inventoryItemPrefab,
                outputSlot.transform
            );

            inventoryItem invItem = newItemGO.GetComponent<inventoryItem>();
            invItem.InitializeItem(recipe.outputItem);
            invItem.count = 1;
            invItem.RefreshCount();
            invItem.parentBeforeDrag = outputSlot.transform;

            outputSlot.SetItem(recipe.outputItem);
        }
        else
        {
            Debug.Log("Output slot full or incompatible. Waiting...");
            isSmelting = false;
            yield break;
        }

        // Clear input/fuel
        inputSlot.ConsumeOne();
        fuelSlot.ConsumeOne();


        isSmelting = false;

        // ✅ Immediately restart smelting if valid new items exist
        if (CanSmelt())
        {
            StartCoroutine(SmeltCoroutine());
        }
    }




}
