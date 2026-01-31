using UnityEngine;
using FishNet.Object;
using TMPro; // for TextMeshProUGUI

public class SkillTreeUIManager : MonoBehaviour
{
    private GameObject activeSkillTreePanel;
    private PlayerRole playerRole;
    private Canvas playerCanvas;
    private bool ownerReady = false;

    private PlayerSkillManager skillManager;
    private SkillPointsUI pointsUI;

    private void Awake()
    {
        playerRole = GetComponentInParent<PlayerRole>();
        playerCanvas = GetComponentInParent<Canvas>();
        skillManager = GetComponentInParent<PlayerSkillManager>();

        Debug.Log($"[SkillTreeUIManager] Awake on '{gameObject.name}'. " +
                  $"playerRole={(playerRole ? playerRole.name : "null")}, canvas={(playerCanvas ? playerCanvas.name : "null")}");
    }

    private void Start()
    {
        if (playerRole == null)
        {
            playerRole = GetComponentInParent<PlayerRole>();
            Debug.Log($"[SkillTreeUIManager] Start found playerRole={(playerRole ? playerRole.name : "null")}");
        }

        if (playerCanvas == null)
        {
            playerCanvas = GetComponentInParent<Canvas>();
            Debug.Log($"[SkillTreeUIManager] Start found canvas={(playerCanvas ? playerCanvas.name : "null")}");
        }

        if (skillManager == null)
        {
            skillManager = GetComponentInParent<PlayerSkillManager>();
        }
    }

    private void Update()
    {
        if (playerRole == null)
        {
            playerRole = GetComponentInParent<PlayerRole>();
            if (playerRole != null)
                Debug.Log($"[SkillTreeUIManager] PlayerRole discovered at runtime: {playerRole.name}");
        }

        if (skillManager == null)
        {
            skillManager = GetComponentInParent<PlayerSkillManager>();
        }

        if (playerRole == null || !playerRole.IsOwner)
        {
            if (ownerReady)
            {
                ownerReady = false;
                Debug.Log("[SkillTreeUIManager] Owner lost or not ready anymore.");
            }
            return;
        }

        if (!ownerReady)
        {
            ownerReady = true;
            Debug.Log($"[SkillTreeUIManager] Owner confirmed for player '{playerRole.name}'. Skill UI is now active.");
        }

        if (playerCanvas == null)
        {
            playerCanvas = GetComponentInParent<Canvas>();
            Debug.Log($"[SkillTreeUIManager] Canvas found at runtime: {(playerCanvas ? playerCanvas.name : "null")}");
            if (playerCanvas == null)
            {
                Debug.LogWarning("[SkillTreeUIManager] Still no canvas found for owner player. Skill tree cannot be opened.");
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[SkillTreeUIManager] K pressed by owner -> toggling skill tree.");
            ToggleSkillTree();
        }
    }

    public void ToggleSkillTree()
    {
        if (!ownerReady)
        {
            Debug.LogWarning("[SkillTreeUIManager] Toggle called but owner not ready. Aborting.");
            return;
        }

        if (activeSkillTreePanel == null)
        {
            RoleData role = playerRole.CurrentRole;
            if (role == null)
            {
                Debug.LogWarning("[SkillTreeUIManager] Cannot open skill tree: playerRole.CurrentRole is null.");
                return;
            }

            if (role.skillTreePanelPrefab == null)
            {
                Debug.LogWarning("[SkillTreeUIManager] No skillTreePanelPrefab assigned on RoleData for role: " + role.roleName);
                return;
            }

            if (playerCanvas == null)
            {
                Debug.LogWarning("[SkillTreeUIManager] playerCanvas is null; cannot spawn skill tree panel.");
                return;
            }

            activeSkillTreePanel = Instantiate(role.skillTreePanelPrefab, playerCanvas.transform);
            activeSkillTreePanel.SetActive(true);

            Debug.Log($"[SkillTreeUIManager] Spawned skill tree panel for role '{role.roleName}' under canvas '{playerCanvas.name}'.");

            // 🔹 Find only the dedicated SkillPointsUI in the prefab
            pointsUI = activeSkillTreePanel.GetComponentInChildren<SkillPointsUI>(true);
            if (pointsUI != null && skillManager != null)
            {
                pointsUI.SetPoints(skillManager.AvailableSkillPoints);
                skillManager.OnSkillPointsChanged += UpdatePointsText;
            }
        }
        else
        {
            Debug.Log("[SkillTreeUIManager] Closing skill tree panel.");
            if (pointsUI != null && skillManager != null)
                skillManager.OnSkillPointsChanged -= UpdatePointsText;

            Destroy(activeSkillTreePanel);
            activeSkillTreePanel = null;
            pointsUI = null;
        }
    }

    private void UpdatePointsText(int newPoints)
    {
        if (pointsUI != null)
            pointsUI.SetPoints(newPoints);
    }
}
