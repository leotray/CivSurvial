using UnityEngine;
using FishNet.Object;
using static Item;

public class PlayerCombatNetworked : NetworkBehaviour
{
    public MPInventoryManager inventoryManager;

    public Transform attackPoint;
    public float attackRange = 2f;
    public LayerMask attackMask;

    private float nextAttackTime = 0f;
    private float nextThrowTime = 0f;

    private PlayerStatsNetworked stats;

    public float NextAttackTime => nextAttackTime;

    [Header("Role Multipliers")]
    public float miningSpeedMultiplier = 1f;     // mining swing speed
    public float miningDamageMultiplier = 1f;    // bonus mining damage
    public float attackSpeedMultiplier = 1f;     // combat swing speed
    public float damageMultiplier = 1f;          // bonus combat damage

    private void Start()
    {
        stats = GetComponent<PlayerStatsNetworked>();
    }

    // NEW: Current attack rate for UI
    public float CurrentAttackRate
    {
        get
        {
            GameObject heldItem = inventoryManager.currentHeldItem;
            if (heldItem == null) return 0f;

            var slot = inventoryManager.slotsUI[inventoryManager.selectedSlot];
            var mpItem = slot.GetComponentInChildren<MPInventoryItem>();
            if (mpItem == null) return 0f;

            var itemData = GameData.LookupItem(inventoryManager.inventory[inventoryManager.selectedSlot].itemGuid);
            if (itemData == null) return 0f;

            // Show the max possible attack rate (combat or mining)
            // Since UI can't know what you'll hit, default to combat multiplier
            return itemData.attackRate * Mathf.Max(attackSpeedMultiplier, miningSpeedMultiplier);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        GameObject heldItem = inventoryManager.currentHeldItem;
        if (heldItem == null) return;

        var slot = inventoryManager.slotsUI[inventoryManager.selectedSlot];
        var mpItem = slot.GetComponentInChildren<MPInventoryItem>();
        if (mpItem == null) return;

        var itemData = GameData.LookupItem(inventoryManager.inventory[inventoryManager.selectedSlot].itemGuid);
        if (itemData == null) return;

        // Left Click = Attack
        if (Time.time >= nextAttackTime && Input.GetMouseButtonDown(0))
        {
            if (!stats.HasStaminaForAttack()) return;
            stats.UseStaminaForAttack();

            // Always use base attack rate for cooldown. 
            float baseCooldown = 1f / itemData.attackRate;
            nextAttackTime = Time.time + baseCooldown;

            AttackServerRpc(
                baseDamage: itemData.damage,
                toolType: (int)itemData.toolType,
                category: (int)itemData.itemCategory,
                effectiveAgainst: (int)itemData.effectiveAgainst,
                multiplier: itemData.effectivenessMultiplier,
                attackerPosition: attackPoint.position,
                baseAttackRate: itemData.attackRate
            );
        }

        // Right Click = Throw
        if (itemData.isThrowable && Time.time >= nextThrowTime && Input.GetMouseButtonDown(1))
        {
            if (stats.currentStamina < itemData.throwStaminaCost) return;

            stats.UseStaminaForAttack(itemData.throwStaminaCost);

            nextThrowTime = Time.time + itemData.throwCooldown;
            ThrowServerRpc(itemData.guid, attackPoint.position, transform.forward);
        }
    }

    [ServerRpc]
    private void AttackServerRpc(
        int baseDamage,
        int toolType,
        int category,
        int effectiveAgainst,
        float multiplier,
        Vector3 attackerPosition,
        float baseAttackRate)
    {
        Collider[] hits = Physics.OverlapSphere(attackerPosition, attackRange);

        bool hitSomething = false;
        foreach (var hit in hits)
        {
            PlayerStatsNetworked otherPlayer = hit.GetComponent<PlayerStatsNetworked>();
            if (otherPlayer != null && otherPlayer == GetComponent<PlayerStatsNetworked>())
                continue;

            if (otherPlayer != null)
            {
                hitSomething = true;
                int finalDamage = baseDamage;

                // Apply item effectiveness vs player
                if ((ItemCategory)category == ItemCategory.Tool &&
                    (ItemData.TargetType)effectiveAgainst == ItemData.TargetType.Player)
                {
                    finalDamage = Mathf.RoundToInt(baseDamage * multiplier);
                }

                // Apply ONLY combat role multipliers (fighter bonus)
                finalDamage = Mathf.RoundToInt(finalDamage * damageMultiplier);

                otherPlayer.TakeDamage(finalDamage, attackerPosition, false);

                // Adjust attack cooldown for combat roles
                float adjustedCooldown = 1f / (baseAttackRate * attackSpeedMultiplier);
                nextAttackTime = Time.time + adjustedCooldown;

                continue;
            }

            // Inside AttackServerRpc()
            if (hit.CompareTag("Mineable-Damageable"))
            {
                var mineable = hit.GetComponent<MPMineableObject>();
                if (mineable != null)
                {
                    hitSomething = true;

                    // Check if it's a banner
                    var banner = mineable.GetComponent<Banner>();
                    if (banner != null)
                    {
                        ulong attackerId = (ulong)Owner.ClientId;
                        int playerCityId = CityManager.Instance.GetPlayerCityId(attackerId);
                        int playerFactionId = CityManager.Instance.GetPlayerFactionId(attackerId);

                        int bannerCityId = banner.CityId;
                        int bannerFactionId = banner.FactionId;

                        // ✅ Cannot attack own city
                        if (playerCityId == bannerCityId)
                        {
                            Debug.Log($"[Combat] Player {attackerId} cannot damage their own city's banner.");
                            continue;
                        }

                        // ✅ Cannot attack banners of same faction
                        if (playerFactionId == bannerFactionId)
                        {
                            Debug.Log($"[Combat] Player {attackerId} cannot damage a banner of their own faction.");
                            continue;
                        }

                        // ✅ Must belong to a faction
                        if (playerFactionId <= 0)
                        {
                            Debug.Log($"[Combat] Player {attackerId} cannot damage banners while not in a faction.");
                            continue;
                        }

                        // ✅ Must be at war
                        if (!WarManager.Instance.IsAtWar(playerFactionId, bannerFactionId))
                        {
                            Debug.Log($"[Combat] Player {attackerId} cannot damage banner of faction {bannerFactionId} — not at war.");
                            continue;
                        }

                        // ✅ Passed all checks: apply damage
                        int finalDamage = baseDamage;
                        if ((ItemCategory)category == ItemCategory.Tool &&
                            (TargetType)effectiveAgainst == mineable.targetType)
                        {
                            finalDamage = Mathf.RoundToInt(baseDamage * multiplier);
                        }

                        finalDamage = Mathf.RoundToInt(finalDamage * miningDamageMultiplier);
                        mineable.takeDamage(finalDamage);

                        // Adjust cooldown
                        float adjustedCooldown = 1f / (baseAttackRate * miningSpeedMultiplier);
                        nextAttackTime = Time.time + adjustedCooldown;

                        continue;
                    }

                    // Non-banner mineable (rocks, trees, etc.)
                    int normalDamage = baseDamage;
                    if ((ItemCategory)category == ItemCategory.Tool &&
                        (TargetType)effectiveAgainst == mineable.targetType)
                    {
                        normalDamage = Mathf.RoundToInt(baseDamage * multiplier);
                    }

                    normalDamage = Mathf.RoundToInt(normalDamage * miningDamageMultiplier);
                    mineable.takeDamage(normalDamage);

                    float normalCooldown = 1f / (baseAttackRate * miningSpeedMultiplier);
                    nextAttackTime = Time.time + normalCooldown;
                }
            }


        }

        // If nothing hit, just use base cooldown
        if (!hitSomething)
            nextAttackTime = Time.time + (1f / baseAttackRate);
    }

    [ServerRpc]
    private void ThrowServerRpc(string guid, Vector3 spawnPos, Vector3 forward)
    {
        ItemData item = GameData.LookupItem(guid);
        if (item == null || item.throwablePrefab == null) return;

        GameObject proj = Instantiate(item.throwablePrefab, spawnPos + forward * 0.5f, Quaternion.identity);
        var rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(forward * item.throwForce, ForceMode.VelocityChange);

        var projScript = proj.GetComponent<ThrowableProjectile>();
        if (projScript != null)
        {
            projScript.damage = item.throwDamage;
            projScript.lifetime = item.throwLifetime;
            projScript.ownerId = Owner.ClientId;
        }

        Spawn(proj, Owner);

        inventoryManager.RemoveItem(guid, 1);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}
