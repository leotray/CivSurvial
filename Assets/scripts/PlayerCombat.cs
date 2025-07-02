using UnityEngine;
using UnityEngine.UIElements;
using static Item;

public class PlayerCombat : MonoBehaviour
{
    public InventoryManager inventoryManager;

    public Transform attackPoint;
    public float attackRange;
    public LayerMask attackMask;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }

        // 👇 NEW: Handle shield blocking pose
        if (Input.GetMouseButtonDown(1))
        {
            inventoryManager.SetShieldBlockingPose(true);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            inventoryManager.SetShieldBlockingPose(false);
        }
    }

    public void Attack()
    {
        GameObject heldItem = inventoryManager.currentHeldItem;
        if (heldItem == null) return;

        inventoryItem selectedItem = inventoryManager.inventorySlots[inventoryManager.selectedSlot]
                                        .GetComponentInChildren<inventoryItem>();
        if (selectedItem == null || selectedItem.item == null) return;

        Item itemData = selectedItem.item;

        Animator animator = heldItem.GetComponent<Animator>();
        if (animator != null)
        {
            string trigger = GetAttackTrigger(itemData.toolType);
            if (!string.IsNullOrEmpty(trigger))
                animator.SetTrigger(trigger);
        }

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, attackRange, attackMask);
        Debug.Log(hitEnemies.Length);
        Debug.Log("attack!");

        int baseDamage = itemData.damage;

        foreach (Collider enemy in hitEnemies)
        {
            MineableObject mineable = enemy.GetComponent<MineableObject>();
            if (mineable != null)
            {
                int finalDamage = baseDamage;

                if (itemData.itemCategory == ItemCategory.Tool &&
                    itemData.effectiveAgainst == mineable.targetType)
                {
                    finalDamage = Mathf.RoundToInt(baseDamage * itemData.effectivenessMultiplier);
                }

                Debug.Log($"Hitting {enemy.name} with {finalDamage} damage");
                mineable.takeDamage(finalDamage);

                Animator rockAnimator = enemy.GetComponent<Animator>();
                if (rockAnimator != null)
                {
                    rockAnimator.SetTrigger("hit");
                }
            }
        }
    }

    private string GetAttackTrigger(ToolType toolType)
    {
        switch (toolType)
        {
            case ToolType.Sword: return "SwordAttack";
            case ToolType.Pickaxe: return "PickaxeSwing";
            case ToolType.Shovel: return "ShovelDig";
            case ToolType.Axe: return "AxeChop";
            default: return null;
        }
    }

    private void OnDrawGizmos()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
