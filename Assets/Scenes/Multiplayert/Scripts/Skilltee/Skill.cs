using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSkill", menuName = "Skills/Skill")]
public class Skill : ScriptableObject
{
    [Tooltip("Unique skill ID")]
    public string skillId;

    public string skillName;
    [TextArea] public string description;
    public Sprite icon;

    [Tooltip("Role that can unlock this skill (e.g., Blacksmith, Farmer)")]
    public string roleId;

    [Tooltip("Faction tech required for this skill to be visible/unlockable")]
    public string requiredTechId;

    [Tooltip("Recipes this skill unlocks (names must match your recipe system)")]
    public List<string> unlockedRecipeNames = new List<string>();

    [Header("Skill Tree Extensions")]
    [Tooltip("Which skills must be unlocked before this one?")]
    public List<Skill> requiredSkills = new List<Skill>();

    [Tooltip("How many skill points does this cost?")]
    public int skillPointCost = 1;
}
