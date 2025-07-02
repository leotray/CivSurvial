using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Object/Item")]
public class Item : ScriptableObject
{
    [Header("General")]
    public string itemName;
    public GameObject itemPrefab;
    public GameObject prefab;
    public Sprite sprite;
    public int itemMaxCount = 1;
    public bool stackable = true;
    public Vector3Int range = new Vector3Int(5, 4, 5);
    public bool isShield; // 👈 Add this to the Item ScriptableObject


    [Header("Type")]
    public ItemCategory itemCategory;

    [Header("Tool Settings")]
    public ToolType toolType;
    public ToolTier toolTier;
    public int baseDamage;
    public TargetType effectiveAgainst;  // Use global enum here!
    public float effectivenessMultiplier = 2f;

    [Header("Combat Stats")]
    [ReadOnly] public int damage;

    [Header("Food Settings")]
    public int hungerRestore;

    public bool isFuel;  // ✅ This marks whether the item is usable as fuel
    private void OnValidate()
    {
        if (itemCategory == ItemCategory.Tool)
        {
            damage = GetDamageForTool(toolType, toolTier);
        }
        else
        {
            damage = 0;
        }
    }

    private int GetDamageForTool(ToolType type, ToolTier tier)
    {

        switch (type)
        {
            case ToolType.Sword:
                return tier switch
                {
                    ToolTier.Stone => 15,
                    ToolTier.Iron => 25,
                    ToolTier.Titanium => 45,
                    ToolTier.Steel => 35,
                    _ => 0
                };
            case ToolType.Pickaxe:
                return tier switch
                {
                    ToolTier.Stone => 10,
                    ToolTier.Iron => 20,
                    ToolTier.Titanium => 40,
                    ToolTier.Steel => 30,
                    _ => 0
                };
            case ToolType.Shovel:
                return tier switch
                {
                    ToolTier.Stone => 1,
                    ToolTier.Iron => 3,
                    ToolTier.Titanium => 5,
                    ToolTier.Steel => 4,
                    _ => 0
                };
            case ToolType.Axe:
                return tier switch
                {
                    ToolTier.Stone => 3,
                    ToolTier.Iron => 5,
                    ToolTier.Titanium => 8,
                    ToolTier.Steel => 6,
                    _ => 0
                };
            default:
                return 0;
        }
    }

    public enum ItemCategory
    {
        Tool,
        Food,
        BuildingBlock,
        CraftingItem
    }

    public enum ToolType
    {
        Sword,
        Shovel,
        Pickaxe,
        Axe
    }

    public enum ToolTier
    {
        Stone,
        Iron,
        Titanium,
        Steel
    }
}

// Optional read-only attribute (can remove if you don’t use it)
public class ReadOnlyAttribute : PropertyAttribute { }
