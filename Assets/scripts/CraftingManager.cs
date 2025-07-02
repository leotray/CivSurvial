using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CraftingManager : MonoBehaviour
{
    public List<CraftingRecipe> allRecipes;
    public InventoryManager inventoryManager;

    public GameObject recipeButtonPrefab;

    [Header("Recipe Containers")]
    public Transform recipeContainer; // Active container, set dynamically
    public Transform defaultPlayerRecipeContainer;

    public List<CraftingRecipe> knownRecipes = new List<CraftingRecipe>();

    void Start()
    {
        // Only refresh default player UI if assigned
        if (defaultPlayerRecipeContainer != null)
        {
            recipeContainer = defaultPlayerRecipeContainer;
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        if (recipeContainer == null)
        {
            Debug.LogWarning("⚠️ No recipe container assigned to CraftingManager.");
            return;
        }

        foreach (Transform child in recipeContainer)
            Destroy(child.gameObject);

        Debug.Log("Refreshing UI for: " + recipeContainer.name);

        foreach (CraftingRecipe recipe in knownRecipes)
        {
            bool isPlayerUI = recipeContainer == defaultPlayerRecipeContainer;
            bool isTableUI = recipeContainer.name.ToLower().Contains("table");
            bool isAnvilUI = recipeContainer.name.ToLower().Contains("anvil");

            if (isPlayerUI && (recipe.requiresCraftingTable || recipe.requiresAnvil)) continue;
            if (isTableUI && !recipe.requiresCraftingTable) continue;
            if (isAnvilUI && !recipe.requiresAnvil) continue;

            GameObject buttonGO = Instantiate(recipeButtonPrefab, recipeContainer);
            Button button = buttonGO.GetComponent<Button>();
            Text label = buttonGO.GetComponentInChildren<Text>();

            label.text = $"Craft {recipe.result.itemName}";
            if (recipe.requiresCraftingTable)
                label.text += " (Table)";
            else if (recipe.requiresAnvil)
                label.text += " (Anvil)";

            CraftingRecipe localRecipe = recipe;
            button.onClick.AddListener(() =>
            {
                bool success = Craft(localRecipe,
                    atCraftingTable: recipe.requiresCraftingTable,
                    atAnvil: recipe.requiresAnvil);

                if (!success)
                    Debug.Log($"❌ Could not craft {localRecipe.result.itemName}");
            });
        }
    }

    public void UnlockRecipe(CraftingRecipe recipe)
    {
        if (recipe == null)
        {
            Debug.LogError("❌ Tried to unlock a NULL recipe.");
            return;
        }

        if (recipe.result == null)
        {
            Debug.LogError($"❌ Recipe '{recipe.name}' has no result assigned!");
            return;
        }

        if (!knownRecipes.Contains(recipe))
        {
            knownRecipes.Add(recipe);
            Debug.Log($"✅ Unlocked recipe: {recipe.result.itemName}");
            RefreshUI();
        }
    }

    public bool Craft(CraftingRecipe recipe, bool atCraftingTable = false, bool atAnvil = false)
    {
        if (recipe.requiresCraftingTable && !atCraftingTable)
        {
            Debug.Log("⚠️ You need to be at a crafting table to craft this item!");
            return false;
        }

        if (recipe.requiresAnvil && !atAnvil)
        {
            Debug.Log("⚠️ You need to be at an anvil to craft this item!");
            return false;
        }

        if (!knownRecipes.Contains(recipe))
        {
            Debug.Log($"❌ Recipe not unlocked: {recipe.result.itemName}");
            return false;
        }

        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            if (CountItem(recipe.ingredients[i]) < recipe.ingredientCounts[i])
            {
                Debug.Log("❌ Not enough ingredients!");
                return false;
            }
        }

        for (int i = 0; i < recipe.ingredients.Length; i++)
            RemoveItems(recipe.ingredients[i], recipe.ingredientCounts[i]);

        for (int i = 0; i < recipe.resultCount; i++)
            inventoryManager.AddItem(recipe.result);

        Debug.Log($"✅ Crafted {recipe.resultCount}x {recipe.result.itemName}");
        return true;
    }

    int CountItem(Item item)
    {
        int total = 0;
        foreach (inventorySlot slot in inventoryManager.inventorySlots)
        {
            inventoryItem invItem = slot.GetComponentInChildren<inventoryItem>();
            if (invItem != null && invItem.item == item)
                total += invItem.count;
        }
        return total;
    }

    void RemoveItems(Item item, int count)
    {
        foreach (inventorySlot slot in inventoryManager.inventorySlots)
        {
            inventoryItem invItem = slot.GetComponentInChildren<inventoryItem>();
            if (invItem != null && invItem.item == item)
            {
                int removeCount = Mathf.Min(invItem.count, count);
                invItem.count -= removeCount;
                count -= removeCount;
                invItem.RefreshCount();

                if (invItem.count <= 0)
                    Destroy(invItem.gameObject);

                if (count <= 0) break;
            }
        }
    }

    public void SetRecipeContainer(Transform container)
    {
        recipeContainer = container;
    }
}
