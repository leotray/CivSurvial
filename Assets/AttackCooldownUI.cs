using UnityEngine;
using UnityEngine.UI;

public class AttackCooldownUI : MonoBehaviour
{
    public Image cooldownImage;
    private PlayerCombatNetworked combat;

    private void Start()
    {
        combat = FindObjectOfType<PlayerCombatNetworked>();
    }

    void Update()
    {
        if (combat == null || combat.CurrentAttackRate <= 0) return;

        float cooldown = 1f / combat.CurrentAttackRate;
        float remaining = Mathf.Clamp(combat.NextAttackTime - Time.time, 0f, cooldown);
        float percent = 1f - (remaining / cooldown);
        cooldownImage.fillAmount = percent;
    }
}
