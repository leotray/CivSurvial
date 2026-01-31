using UnityEngine;

[CreateAssetMenu(menuName = "Roles/New Role", fileName = "NewRole")]
public class RoleData : ScriptableObject
{
    [Header("Basic Info")]
    public string roleName;
    public Sprite icon;
    public string description;

    [Header("Stat Modifiers")]
    public float miningSpeedMultiplier = 1f;
    public float miningDamageMultiplier = 1f;
    public float combatAttackSpeedMultiplier = 1f;
    public float combatDamageMultiplier = 1f;
    public int maxHealthBonus = 0;
    public float staminaRegenBonus = 0f;

    [Header("Building")]
    public int buildingCostMultiplier = 2;

    [Header("Skill Tree")]
    public GameObject skillTreePanelPrefab; // assign the panel prefab for this role
}
