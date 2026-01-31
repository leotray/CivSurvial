using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillButton : MonoBehaviour
{
    public string skillId;              // e.g. "0", "1", "2" etc.

    [Header("UI References")]
    public Button button;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI statusText;

    private Skill skillAsset;
    private PlayerSkillManager skillManager;
    private PlayerRole playerRole;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void Start()
    {
        // Get references
        playerRole = PlayerRole.LocalPlayerRole;
        if (playerRole != null)
        {
            skillManager = playerRole.GetComponent<PlayerSkillManager>();
            if (skillManager != null)
            {
                skillAsset = skillManager.SkillDatabase.GetById(skillId);
            }
        }

        RefreshState();
    }

    private void OnEnable()
    {
        RefreshState();
    }

    public void RefreshState()
    {
        if (playerRole == null) playerRole = PlayerRole.LocalPlayerRole;
        if (playerRole == null) return;

        if (skillManager == null) skillManager = playerRole.GetComponent<PlayerSkillManager>();
        if (skillManager == null) return;

        if (skillAsset == null) skillAsset = skillManager.SkillDatabase.GetById(skillId);
        if (skillAsset == null)
        {
            Debug.LogWarning($"[SkillButton] Skill '{skillId}' not found in database!");
            button.interactable = false;
            if (statusText != null) statusText.text = "Skill Not Found";
            return;
        }

        // Update skill name
        if (skillNameText != null) skillNameText.text = skillAsset.skillName;

        // Check if skill is already unlocked
        bool alreadyUnlocked = skillManager.HasSkill(skillId);
        if (alreadyUnlocked)
        {
            button.interactable = false;
            if (statusText != null) statusText.text = "✅ UNLOCKED";
            return;
        }

        // Check role compatibility - FIXED: Use roleName instead of roleId
        string currentRoleName = playerRole.CurrentRole?.roleName;
        if (string.IsNullOrEmpty(currentRoleName) || skillAsset.roleId != currentRoleName)
        {
            button.interactable = false;
            if (statusText != null) statusText.text = $"Requires {skillAsset.roleId} Role";
            return;
        }

        // Check if faction tech requirement is met
        bool canUnlock = skillManager.CanUnlockSkill(skillAsset);
        button.interactable = canUnlock;

        if (statusText != null)
        {
            if (canUnlock)
            {
                statusText.text = "Click to Unlock";
            }
            else
            {
                string requiredTech = skillAsset.requiredTechId;
                statusText.text = string.IsNullOrEmpty(requiredTech) ? "Cannot Unlock" : $"Requires {requiredTech} Tech";
            }
        }

        Debug.Log($"[SkillButton] Skill '{skillAsset.skillName}' - CanUnlock: {canUnlock}, AlreadyUnlocked: {alreadyUnlocked}");
    }

    private void OnClick()
    {
        if (playerRole == null || skillManager == null || skillAsset == null) return;

        // Double-check requirements before sending ServerRpc
        if (skillManager.HasSkill(skillId))
        {
            Debug.LogWarning($"[SkillButton] Skill '{skillId}' already unlocked!");
            RefreshState();
            return;
        }

        if (!skillManager.CanUnlockSkill(skillAsset))
        {
            Debug.LogWarning($"[SkillButton] Cannot unlock skill '{skillId}' - requirements not met!");
            RefreshState();
            return;
        }

        Debug.Log($"[SkillButton] Requesting unlock for skill '{skillAsset.skillName}' (ID: {skillId})");

        // Request unlock from server
        skillManager.UnlockSkillServerRpc(skillId);

        // Refresh immediately to show loading state
        RefreshState();
    }
}
