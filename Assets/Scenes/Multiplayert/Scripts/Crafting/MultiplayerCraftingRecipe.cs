// Assets/Scripts/Crafting/MultiplayerCraftingRecipe.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMultiplayerRecipe", menuName = "Crafting/Multiplayer Recipe")]
public class MultiplayerCraftingRecipe : ScriptableObject
{
    public string recipeName;
    public List<Ingredient> ingredients = new();
    public ItemData outputItem;

    public int outputAmount = 1;

    [System.Serializable]
    public class Ingredient
    {
        public string itemGuid;
        public int amount;
    }
}
