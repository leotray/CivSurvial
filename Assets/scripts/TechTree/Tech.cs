using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tech", menuName = "Tech Tree/Tech")]
public class Tech : ScriptableObject
{
    public string techName;
    public int cost;
    public string description; // 🆕 add this
    public List<Tech> unlocksAfterPurchase;
    public List<CraftingRecipe> unlockedRecipes;
}

