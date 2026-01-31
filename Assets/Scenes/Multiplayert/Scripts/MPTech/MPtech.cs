// Tech.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTech", menuName = "MPTech/MPTech")]
public class MPtech : ScriptableObject
{
    [Tooltip("Unique ID for this tech (string)")]
    public string techId;

    public string techName;
    public int cost = 1;

    [Tooltip("List of multiplayer recipe names (strings) that this tech unlocks")]
    public List<string> unlockedRecipeNames = new List<string>();

    [Tooltip("Optional: next techs to reveal after purchasing")]
    public List<MPtech> unlocksAfterPurchase = new List<MPtech>();

    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("Techs required before this tech can be unlocked")]
    public List<MPtech> prerequisites = new List<MPtech>();

}
