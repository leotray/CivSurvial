using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Combat Settings")]
    public float attackRate = 1f; // attacks per second

    [Header("Identifiers")]
    public string guid; // Unique ID
    public string itemName;

    [Header("Visuals")]
    public Sprite icon;
    public GameObject itemPrefab; // held-in-hand prefab
    [Tooltip("Prefab used for building placement / preview")]
    public GameObject buildPrefab; // NEW: prefab to spawn/preview when building

    [Header("Stacking & General")]
    public int itemMaxCount = 1;
    public bool stackable = true;
    public Vector3Int range = new Vector3Int(5, 4, 5);

    [Header("Category")]
    public ItemCategory itemCategory;

    [Header("Building")]
    public BuildableType buildType = BuildableType.None; // NEW: what type of build (floor/wall/etc)

    [Header("Tool Settings")]
    public ToolType toolType;
    public ToolTier toolTier;
    public int baseDamage;
    public TargetType effectiveAgainst;
    public float effectivenessMultiplier = 2f;

    [Header("Combat Stats")]
    [ReadOnly] public int damage;

    [Header("Food Settings")]
    public int hungerRestore;

    [Header("Special Flags")]
    public bool isShield;
    public bool tamesHorses;
    public bool isFuel;

    [Header("Throwable Settings")] // NEW
    public bool isThrowable;                // Can this item be thrown?
    public GameObject throwablePrefab;      // Prefab that flies when thrown
    public float throwForce = 15f;          // How strong it is thrown
    public float throwCooldown = 0.5f;      // Time before you can throw again
    public int throwDamage = 10;            // Damage on hit
    public float throwLifetime = 5f;        // Destroy projectile after X seconds
    public int throwStaminaCost = 15;       // Cost of throwing

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

    public enum BuildableType
    {
        None,
        Floor,
        Wall,
        Roof,
        Stairs
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

    public enum TargetType
    {
        None,
        Rock,
        Tree,
        Enemy,
        Player
    }
}

// Optional read-only attribute
public class ReadOnlyAttribute : PropertyAttribute { }
