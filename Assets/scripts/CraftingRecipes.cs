using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Object/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public Item[] ingredients;
    public int[] ingredientCounts;
    public Item result;
    public int resultCount = 1;

    // ✅ New: requires crafting table
    public bool requiresCraftingTable = false;
    public bool requiresAnvil = false;

}

