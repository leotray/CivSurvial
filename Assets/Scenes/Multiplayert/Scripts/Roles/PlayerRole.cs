using FishNet.Object;
using UnityEngine;

public class PlayerRole : NetworkBehaviour
{
    [SerializeField] private RoleDatabase roleDatabase;
    public RoleData CurrentRole { get; private set; }

    public static PlayerRole LocalPlayerRole; // reference to local player’s role component (kept for compatibility)

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            // Keep the static reference for compatibility with other code that might use it.
            LocalPlayerRole = this;

            // Find the RoleSelectionUI that is part of this player's prefab (player canvas).
            // Use 'true' to include inactive objects (panel starts inactive).
            RoleSelectionUI roleUI = GetComponentInChildren<RoleSelectionUI>(true);
            if (roleUI != null)
            {
                roleUI.Open(roleDatabase, this);
            }
            else
            {
                Debug.LogWarning("[PlayerRole] RoleSelectionUI not found on player prefab. Make sure the RoleSelectionUI component is placed on the player's canvas prefab.");
            }
        }
    }

    [ServerRpc]
    public void SelectRoleServerRpc(int roleIndex)
    {
        if (roleIndex < 0 || roleIndex >= roleDatabase.roles.Length) return;

        CurrentRole = roleDatabase.roles[roleIndex];
        ApplyRoleBonuses();
        SelectRoleObserverRpc(roleIndex);
    }

    [ObserversRpc]
    private void SelectRoleObserverRpc(int roleIndex)
    {
        CurrentRole = roleDatabase.roles[roleIndex];
        ApplyRoleBonuses();
    }

    private void ApplyRoleBonuses()
    {
        if (CurrentRole == null) return;

        var stats = GetComponent<PlayerStatsNetworked>();
        if (stats != null)
        {
            stats.maxHealth += CurrentRole.maxHealthBonus;
            stats.staminaRegenMultiplier += CurrentRole.staminaRegenBonus;
        }

        var combat = GetComponent<PlayerCombatNetworked>();
        if (combat != null)
        {
            combat.miningSpeedMultiplier = CurrentRole.miningSpeedMultiplier;
            combat.miningDamageMultiplier = CurrentRole.miningDamageMultiplier;
            combat.attackSpeedMultiplier = CurrentRole.combatAttackSpeedMultiplier;
            combat.damageMultiplier = CurrentRole.combatDamageMultiplier;
        }

        var building = GetComponent<NetworkedBuildingSystem>();
        if (building != null)
        {
            building.buildingCostMultiplier = CurrentRole.buildingCostMultiplier;
        }
    }
}
