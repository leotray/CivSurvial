using UnityEngine;
using UnityEngine.UI;

public class CraftingUI : MonoBehaviour
{
    public CraftingManager craftingManager;
    public Transform recipeButtonContainer;
    public GameObject recipeButtonPrefab;

    [Tooltip("If true, show only recipes that require a crafting table.")]
    public bool isCraftingTableUI = false;

    public bool isAnvilUI = false; // ✅ New toggle in the inspector

    public void RefreshUI()
    {
        foreach (Transform child in recipeButtonContainer)
        {
            Destroy(child.gameObject);
        }

        Debug.Log($"Refreshing UI. Is Anvil: {isAnvilUI}. Known Recipes: {craftingManager.knownRecipes.Count}");

        foreach (CraftingRecipe recipe in craftingManager.knownRecipes)
        {
            Debug.Log($"Checking recipe: {recipe.name} | requiresAnvil: {recipe.requiresAnvil}");

            // Only show if this UI type matches recipe type
            if (isAnvilUI && !recipe.requiresAnvil) continue;
            if (!isAnvilUI && recipe.requiresAnvil) continue;

            GameObject buttonGO = Instantiate(recipeButtonPrefab, recipeButtonContainer);
            Button button = buttonGO.GetComponent<Button>();
            Text label = buttonGO.GetComponentInChildren<Text>();

            label.text = $"Craft {recipe.result.itemName}";

            CraftingRecipe localRecipe = recipe;
            button.onClick.AddListener(() =>
            {
                bool success = craftingManager.Craft(localRecipe,atCraftingTable: isCraftingTableUI, atAnvil: isAnvilUI);

                if (!success)
                    Debug.Log($"❌ Could not craft {localRecipe.result.itemName}");
            });
        }
    }

    private void Start()
    {
        RefreshUI(); // Optional: Remove if you only want manual refresh
    }
}
