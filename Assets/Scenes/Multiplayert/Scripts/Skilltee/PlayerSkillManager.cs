using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class PlayerSkillManager : NetworkBehaviour
{
    [SerializeField] private SkillDatabase skillDatabase;

    [Header("Skill Points")]
    [SerializeField] private int startingSkillPoints = 5;
    private int availableSkillPoints;

    // 👇 Expose as read-only for external systems
    public SkillDatabase SkillDatabase => skillDatabase;
    public int AvailableSkillPoints => availableSkillPoints;

    private HashSet<string> unlockedSkills = new HashSet<string>();

    // Event to notify UI when points change
    public event System.Action<int> OnSkillPointsChanged;

    public bool HasSkill(string skillId) => unlockedSkills.Contains(skillId);

    private void Awake()
    {
        availableSkillPoints = startingSkillPoints;
    }

    private void SetSkillPoints(int newValue)
    {
        availableSkillPoints = newValue;
        OnSkillPointsChanged?.Invoke(availableSkillPoints);
    }

    [ServerRpc]
    public void UnlockSkillServerRpc(string skillId)
    {
        if (unlockedSkills.Contains(skillId))
        {
            Debug.LogWarning($"[PlayerSkillManager] Skill '{skillId}' already unlocked on server!");
            return;
        }

        var skillAsset = skillDatabase.GetById(skillId);
        if (skillAsset == null)
        {
            Debug.LogWarning($"[PlayerSkillManager] Skill '{skillId}' not found in database!");
            return;
        }

        // Validation
        if (!CanUnlockSkill(skillAsset))
        {
            Debug.LogWarning($"[PlayerSkillManager] Cannot unlock skill '{skillId}' - requirements not met!");
            return;
        }

        // Spend skill points
        SetSkillPoints(availableSkillPoints - skillAsset.skillPointCost);

        // Unlock the skill
        unlockedSkills.Add(skillId);

        // Inform client
        TargetSkillUnlocked(Owner, skillId, availableSkillPoints);

        // Handle recipes unlocked by this skill
        if (skillAsset.unlockedRecipeNames.Count > 0)
        {
            var crafting = GetComponent<MultiplayerCraftingManager>();
            if (crafting != null)
            {
                TargetSyncSkillRecipes(Owner, skillAsset.unlockedRecipeNames.ToArray());
            }
        }

        Debug.Log($"[PlayerSkillManager] Server: Successfully unlocked skill '{skillAsset.skillName}' | RemainingPoints={availableSkillPoints}");
    }

    [TargetRpc]
    private void TargetSyncSkillRecipes(FishNet.Connection.NetworkConnection target, string[] recipeNames)
    {
        var localPlayerObj = target.FirstObject.gameObject;
        var crafting = localPlayerObj.GetComponent<MultiplayerCraftingManager>();
        if (crafting != null)
        {
            crafting.ClientUnlockSkillRecipes(recipeNames);
            Debug.Log($"[PlayerSkillManager->Client] Synced {recipeNames.Length} skill recipes to {localPlayerObj.name}.");
        }
    }

    [TargetRpc]
    private void TargetSkillUnlocked(FishNet.Connection.NetworkConnection conn, string skillId, int newSkillPoints)
    {
        if (!unlockedSkills.Contains(skillId))
        {
            unlockedSkills.Add(skillId);
            SetSkillPoints(newSkillPoints);
            Debug.Log($"[PlayerSkillManager->Client] Skill {skillId} unlocked locally! Remaining points: {availableSkillPoints}");
        }
        else
        {
            SetSkillPoints(newSkillPoints);
        }
    }

    /// <summary>
    /// Check if a skill can be unlocked: faction tech + role + prerequisites + points.
    /// </summary>
    public bool CanUnlockSkill(Skill skill)
    {
        var playerRole = GetComponent<PlayerRole>();
        int factionId = FactionManager.Instance.GetFactionIdForPlayer(playerRole);

        // Must be in a faction
        if (factionId < 0) return false;

        // Must have correct role
        if (playerRole?.CurrentRole?.roleName != skill.roleId) return false;

        // Must have required tech unlocked by faction
        if (!string.IsNullOrEmpty(skill.requiredTechId))
        {
            List<string> unlocked = FactionManager.Instance.GetFactionUnlocked(factionId);
            if (!unlocked.Contains(skill.requiredTechId))
                return false;
        }

        // Must have all prerequisite skills unlocked
        foreach (var prereq in skill.requiredSkills)
        {
            if (prereq == null) continue;
            if (!HasSkill(prereq.skillId))
                return false;
        }

        // Must have enough skill points
        if (availableSkillPoints < skill.skillPointCost)
            return false;

        return true;
    }

    public IEnumerable<string> GetUnlockedSkillIds() => unlockedSkills;

    // Utility to grant points (eg. on level up, quest reward, etc.)
    [Server]
    public void AddSkillPoints(int amount)
    {
        SetSkillPoints(availableSkillPoints + amount);
        TargetUpdateSkillPoints(Owner, availableSkillPoints);
    }

    [TargetRpc]
    private void TargetUpdateSkillPoints(FishNet.Connection.NetworkConnection conn, int newPoints)
    {
        SetSkillPoints(newPoints);
        Debug.Log($"[PlayerSkillManager->Client] Updated skill points: {availableSkillPoints}");
    }
}
