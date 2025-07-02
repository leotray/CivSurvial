using System.Collections.Generic;
using UnityEngine;

public class TechTreeManager : MonoBehaviour
{
    public int playerTechPoints = 10;
    public CraftingManager craftingManager;

    private HashSet<Tech> unlockedTechs = new HashSet<Tech>();

    public bool TryPurchaseTech(Tech tech)
    {
        if (unlockedTechs.Contains(tech)) return false;
        if (playerTechPoints < tech.cost) return false;

        playerTechPoints -= tech.cost;
        unlockedTechs.Add(tech);

        Debug.Log($"✅ Unlocked new tech: {tech.techName}. Remaining Tech Points: {playerTechPoints}");

        foreach (var recipe in tech.unlockedRecipes)
        {
            if (recipe != null)
            {
                Debug.Log($"🧪 Unlocked recipe: {recipe.result.itemName}");
                craftingManager.UnlockRecipe(recipe);
            }
            else
            {
                Debug.LogWarning($"⚠️ Null recipe found in tech: {tech.techName}");
            }
        }

        // Debug to check what techs should be revealed
        if (tech.unlocksAfterPurchase.Count == 0)
        {
            Debug.LogWarning($"⚠️ Tech '{tech.techName}' has no next techs to unlock!");
        }
        else
        {
            foreach (var next in tech.unlocksAfterPurchase)
            {
                if (next == null)
                    Debug.LogError($"❌ Tech '{tech.techName}' has a NULL entry in unlocksAfterPurchase!");
                else
                    Debug.Log($"➡️ Tech '{tech.techName}' should unlock next tech: {next.techName}");
            }
        }
        Debug.Log($"🧠 RevealNextTechs for '{tech.techName}' with {tech.unlocksAfterPurchase.Count} follow-ups.");

        // Reveal next tech buttons (auto)
        RevealNextTechs(tech);

        // Refresh crafting UI
        CraftingUI playerUI = FindObjectOfType<CraftingUI>();
        if (playerUI != null) playerUI.RefreshUI();

        return true;
    }

    public bool IsTechUnlocked(Tech tech)
    {
        return unlockedTechs.Contains(tech);
    }

    private void RevealNextTechs(Tech tech)
    {
        foreach (Tech next in tech.unlocksAfterPurchase)
        {
            bool found = false;

            TechButton[] buttons = FindObjectsOfType<TechButton>(true); // include inactive
            foreach (TechButton tb in buttons)
            {
                if (tb.tech == next)
                {
                    Debug.Log($"🔓 Revealing next tech: {next.techName}");
                    tb.gameObject.SetActive(true);
                    tb.Init(this);
                    found = true;
                }
            }

            if (!found)
                Debug.LogWarning($"⚠️ No TechButton found for next tech: {next.techName}. Is it in the scene and assigned?");
        }
    }
}
