using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MultiplayerCraftingUI : MonoBehaviour
{
    public GameObject recipeButtonPrefab;
    public Transform recipeListContainer;

    public MultiplayerCraftingManager localCraftingManager;




    public void RefreshUI()
    {
        Debug.Log("🔄 Refreshing Crafting UI...");

        foreach (Transform child in recipeListContainer)
            Destroy(child.gameObject);

        if (localCraftingManager == null)
        {
            Debug.LogError("❌ localCraftingManager is null in RefreshUI!");
            return;
        }

        if (localCraftingManager.allRecipes == null)
        {
            Debug.LogError("❌ allRecipes is null!");
            return;
        }

        foreach (var recipe in localCraftingManager.allRecipes)
        {
            Debug.Log($"📦 Adding recipe: {recipe.recipeName}");

            GameObject buttonObj = Instantiate(recipeButtonPrefab, recipeListContainer);

            TMP_Text text = buttonObj.GetComponentInChildren<TMP_Text>();
            if (text == null)
                Debug.LogError("❌ TMP_Text not found in button prefab!");
            else
                text.text = recipe.recipeName;

            // Use GetComponentInChildren in case Button is not on the root
            Button btn = buttonObj.GetComponentInChildren<Button>();
            if (btn == null)
            {
                Debug.LogError("❌ Button component not found in prefab or its children!");
            }
            else
            {
                string recipeId = recipe.recipeName;
                btn.onClick.AddListener(() =>
                {
                    Debug.Log($"👆 Button clicked: {recipeId}");
                    localCraftingManager.TryCraft(recipeId);
                });
            }
        }
    }

}