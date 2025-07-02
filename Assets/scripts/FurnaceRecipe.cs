using UnityEngine;

[CreateAssetMenu(menuName = "Crafting/Furnace Recipe")]
public class FurnaceRecipe : ScriptableObject
{
    public Item inputItem;
    public Item outputItem;
    public float smeltTime = 3f;
}
