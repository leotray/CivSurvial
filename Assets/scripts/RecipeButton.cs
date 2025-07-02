using UnityEngine;
using UnityEngine.UI;

public class RecipeButton : MonoBehaviour
{
    public CraftingRecipe recipe;
    public CraftingManager craftingManager;
    public Text buttonText;

    public void Init(CraftingRecipe newRecipe, CraftingManager manager)
    {
        recipe = newRecipe;
        craftingManager = manager;
        buttonText.text = recipe.result.itemName;

        GetComponent<Button>().onClick.AddListener(() =>
        {
            craftingManager.Craft(recipe, atCraftingTable: true);
        });
    }
}
