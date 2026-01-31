using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class MultiplayerCraftingManager : NetworkBehaviour
{
    public List<MultiplayerCraftingRecipe> allRecipes;

    public bool IsClientInitialized { get; private set; } = false;

    // FIXED: Separate tracking for faction and skill recipes
    private HashSet<string> factionRecipeNames = new HashSet<string>();
    private HashSet<string> skillRecipeNames = new HashSet<string>();

    // Combined known recipes (faction + skill)
    private HashSet<string> knownRecipeNames = new HashSet<string>();

    // Optional event for UI hooks
    public event Action OnKnownRecipesChanged;

    // ✅ Reference to TechDatabase ScriptableObject (assign in Inspector)
    [SerializeField] private TechDatabase techDatabase;

    void Awake()
    {
        Debug.Log($"{name} - CraftingManager Awake()");
        if (TryGetComponent<NetworkObject>(out var netObj))
            Debug.Log("✅ CraftingManager is on the same GameObject as NetworkObject.");
        else
            Debug.LogWarning("❌ CraftingManager is NOT on same GameObject as NetworkObject.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"🧩 CraftingManager OnStartClient | IsOwner={IsOwner}, IsClient={IsClient}");
        IsClientInitialized = true;
    }

    // ===========================
    // Craft Request
    // ===========================

    public void TryCraft(string recipeName)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("❌ Cannot craft — not owner.");
            return;
        }

        if (!IsRecipeKnown(recipeName))
        {
            Debug.LogWarning($"❌ Recipe '{recipeName}' is not known/unlocked for your faction or skills.");
            return;
        }

        MultiplayerCraftingRecipe recipe = FindRecipeByName(recipeName);
        if (recipe == null)
        {
            Debug.LogWarning("❌ Recipe not found: " + recipeName);
            return;
        }

        Debug.Log("📨 Sending ServerRpc to try craft: " + recipeName);
        TryCraftServerRpc(recipeName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TryCraftServerRpc(string recipeName, NetworkConnection sender = null)
    {
        ulong playerId = (ulong)sender.ClientId;
        Debug.Log($"🌐 [ServerRpc] Received TryCraft for: {recipeName} from player {playerId}");

        // Find the recipe by name
        MultiplayerCraftingRecipe recipe = FindRecipeByName(recipeName);
        if (recipe == null)
        {
            Debug.LogWarning("❌ Server: Recipe not found: " + recipeName);
            return;
        }

        // Resolve player's inventory, role, and skill manager on the server
        MPInventoryManager inv = null;
        PlayerRole role = null;
        PlayerSkillManager skillManager = null;
        if (ServerManager.Clients.TryGetValue((int)playerId, out var conn))
        {
            var playerObj = conn.FirstObject;
            inv = playerObj?.GetComponent<MPInventoryManager>();
            role = playerObj?.GetComponent<PlayerRole>();
            skillManager = playerObj?.GetComponent<PlayerSkillManager>();
        }
        if (inv == null)
        {
            Debug.LogWarning($"❌ Server: Could not find MPInventoryManager for player {playerId}");
            return;
        }

        // Check for skill unlock
        bool hasSkillUnlock = false;
        if (skillManager != null)
        {
            foreach (string skillId in skillManager.GetUnlockedSkillIds())
            {
                var skill = skillManager.SkillDatabase.GetById(skillId);
                if (skill != null && skill.unlockedRecipeNames.Contains(recipeName))
                {
                    hasSkillUnlock = true;
                    break;
                }
            }
        }

        // Check for tech unlock (from faction)
        int factionId = FactionManager.Instance.GetFactionIdForPlayer(role);
        bool hasTechUnlock = FactionManager.Instance.GetFactionUnlocked(factionId)
            .Select(techId => FactionManager.Instance.techDatabase.GetById(techId))
            .Any(tech => tech != null && tech.unlockedRecipeNames.Contains(recipeName));

        // **Require both skill AND tech unlocks**; log if missing either
        if (!hasSkillUnlock || !hasTechUnlock)
        {
            Debug.LogWarning($"❌ Server: Recipe '{recipeName}' is locked. SkillUnlock={hasSkillUnlock}, TechUnlock={hasTechUnlock}");
            return;
        }

        // Verify player has required items
        if (!HasRequiredItems(inv, recipe))
        {
            Debug.Log("❌ Not enough items to craft.");
            return;
        }

        // Perform crafting: consume ingredients and give result
        Debug.Log("✅ Crafting item on server...");
        ConsumeIngredients(inv, recipe);
        GiveResult(inv, recipe);
    }

    private MultiplayerCraftingRecipe FindRecipeByName(string name)
    {
        return allRecipes.Find(r => r.recipeName == name);
    }

    // ===========================
    // Inventory Operations
    // ===========================

    private bool HasRequiredItems(MPInventoryManager inv, MultiplayerCraftingRecipe recipe)
    {
        Dictionary<string, int> playerItems = new();

        foreach (var item in inv.inventory)
        {
            if (playerItems.ContainsKey(item.itemGuid))
                playerItems[item.itemGuid] += item.count;
            else
                playerItems[item.itemGuid] = item.count;
        }

        foreach (var ingredient in recipe.ingredients)
        {
            if (!playerItems.ContainsKey(ingredient.itemGuid) || playerItems[ingredient.itemGuid] < ingredient.amount)
                return false;
        }

        return true;
    }

    private void ConsumeIngredients(MPInventoryManager inv, MultiplayerCraftingRecipe recipe)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            int remaining = ingredient.amount;

            for (int i = 0; i < inv.inventory.Count && remaining > 0; i++)
            {
                var item = inv.inventory[i];

                if (item.itemGuid == ingredient.itemGuid)
                {
                    int toRemove = Mathf.Min(item.count, remaining);
                    item.count -= toRemove;
                    remaining -= toRemove;

                    if (item.count <= 0)
                        inv.inventory.RemoveAt(i--);
                    else
                        inv.inventory[i] = item;
                }
            }
        }

        Debug.Log($"[Crafting] Consumed ingredients for {recipe.recipeName}");
    }

    private void GiveResult(MPInventoryManager inv, MultiplayerCraftingRecipe recipe)
    {
        string craftedItemGuid = recipe.outputItem.guid;
        Debug.Log($"🎁 Giving crafted item: {craftedItemGuid} x{recipe.outputAmount} to player inventory (server-side)");
        inv.AddItemServer(craftedItemGuid, recipe.outputAmount);
    }

    // ===========================
    // FIXED: Client-side recipe management
    // ===========================

    /// <summary>
    /// FIXED: Called by FactionManager to sync faction recipes
    /// </summary>
    public void ClientUnlockFactionRecipes(string[] recipeNames)
    {
        if (!IsClient) return;

        int added = 0;
        foreach (var recipeName in recipeNames)
        {
            if (string.IsNullOrEmpty(recipeName)) continue;

            if (factionRecipeNames.Add(recipeName))
                added++;
        }

        if (added > 0)
        {
            Debug.Log($"[Crafting] ✅ Learned {added} new FACTION recipes. Total faction recipes: {factionRecipeNames.Count}");
            RefreshKnownRecipes();
        }
    }

    /// <summary>
    /// FIXED: Called by PlayerSkillManager to sync skill recipes
    /// </summary>
    public void ClientUnlockSkillRecipes(string[] recipeNames)
    {
        if (!IsClient) return;

        int added = 0;
        foreach (var recipeName in recipeNames)
        {
            if (string.IsNullOrEmpty(recipeName)) continue;

            if (skillRecipeNames.Add(recipeName))
                added++;
        }

        if (added > 0)
        {
            Debug.Log($"[Crafting] ✅ Learned {added} new SKILL recipes. Total skill recipes: {skillRecipeNames.Count}");
            RefreshKnownRecipes();
        }
    }

    /// <summary>
    /// LEGACY: Called by old system - we'll determine which type based on content
    /// </summary>
    public void ClientUnlockRecipes(string[] recipeNames)
    {
        // FIXED: For backward compatibility, treat as faction recipes
        ClientUnlockFactionRecipes(recipeNames);
    }

    /// <summary>
    /// FIXED: Recipes require BOTH faction tech AND player skill if they appear in both lists
    /// </summary>
    private void RefreshKnownRecipes()
    {
        Debug.Log($"[Crafting] 🔄 RefreshKnownRecipes called - Faction: {factionRecipeNames.Count}, Skills: {skillRecipeNames.Count}");

        var previousCount = knownRecipeNames.Count;
        knownRecipeNames.Clear();

        // FIXED: Get local player components directly from this GameObject instead of static reference
        if (!IsOwner)
        {
            Debug.LogWarning("[Crafting] RefreshKnownRecipes: not owner, skipping refresh");
            return;
        }

        var player = GetComponent<PlayerRole>();
        var skillManager = GetComponent<PlayerSkillManager>();

        if (player == null)
        {
            Debug.LogWarning("[Crafting] RefreshKnownRecipes: PlayerRole component not found on this GameObject");
            return;
        }

        if (skillManager == null)
        {
            Debug.LogWarning("[Crafting] RefreshKnownRecipes: PlayerSkillManager component not found on this GameObject");
            return;
        }

        // FIXED: Handle case where player might not be in faction yet
        int factionId = FactionManager.Instance.GetFactionIdForPlayer(player);
        var factionUnlockedTechs = new List<string>();

        if (factionId >= 0)
        {
            factionUnlockedTechs = FactionManager.Instance.GetFactionUnlocked(factionId);
        }

        Debug.Log($"[Crafting] Faction ID: {factionId}, Unlocked Techs: {factionUnlockedTechs.Count}");

        // Get all recipes that should be checked (combine faction and skill recipes)
        var allPossibleRecipes = new HashSet<string>(factionRecipeNames);
        foreach (var recipe in skillRecipeNames)
            allPossibleRecipes.Add(recipe);

        Debug.Log($"[Crafting] Checking {allPossibleRecipes.Count} possible recipes");

        foreach (var recipeName in allPossibleRecipes)
        {
            bool isInFactionRecipes = factionRecipeNames.Contains(recipeName);
            bool isInSkillRecipes = skillRecipeNames.Contains(recipeName);

            // FIXED: Determine what this recipe actually requires
            bool needsFactionTech = isInFactionRecipes;
            bool needsSkill = isInSkillRecipes;

            bool hasFactionTech = true;
            bool hasSkill = true;

            // Check faction tech requirement
            if (needsFactionTech && factionId >= 0)
            {
                hasFactionTech = factionUnlockedTechs
                    .Select(id => FactionManager.Instance.techDatabase.GetById(id))
                    .Any(tech => tech != null && tech.unlockedRecipeNames.Contains(recipeName));
            }
            else if (needsFactionTech && factionId < 0)
            {
                // Player not in faction but recipe needs faction tech
                hasFactionTech = false;
            }

            // Check skill requirement
            if (needsSkill)
            {
                hasSkill = skillManager.GetUnlockedSkillIds()
                    .Select(id => skillManager.SkillDatabase.GetById(id))
                    .Any(skill => skill != null && skill.unlockedRecipeNames.Contains(recipeName));
            }

            // FIXED: Recipe is available only if it meets ALL its requirements
            bool isAvailable = hasFactionTech && hasSkill;

            Debug.Log($"[Crafting] Recipe '{recipeName}' - NeedsTech:{needsFactionTech} HasTech:{hasFactionTech} NeedsSkill:{needsSkill} HasSkill:{hasSkill} Available:{isAvailable}");

            if (isAvailable)
            {
                knownRecipeNames.Add(recipeName);
            }
        }

        var newCount = knownRecipeNames.Count;
        Debug.Log($"[Crafting] 🔄 Refreshed known recipes: {newCount} total (was {previousCount})");

        if (newCount != previousCount)
        {
            OnKnownRecipesChanged?.Invoke();
        }
    }

    public bool IsRecipeKnown(string recipeName)
    {
        bool known = knownRecipeNames.Contains(recipeName);
        Debug.Log($"[Crafting] IsRecipeKnown('{recipeName}') = {known} (total known: {knownRecipeNames.Count})");
        return known;
    }

    public List<MultiplayerCraftingRecipe> GetKnownRecipes()
    {
        return allRecipes.Where(r => r != null && knownRecipeNames.Contains(r.recipeName)).ToList();
    }
}
