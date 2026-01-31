using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet;

public class ClientFactionUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public TextMeshProUGUI factionNameText;
    public Transform contentParent;   // scroll view content
    public GameObject entryPrefab;    // prefab with PlayerListEntryUI
    public Button leaveButton;
    public Button closeButton;

    [Header("War UI")]
    public TMP_Dropdown warTargetDropdown;
    public Button declareWarButton;

    private int currentFactionId = -1;
    private bool isLeader = false;
    private List<int> currentDropdownFactionIds = new List<int>();

    private PlayerFactionBridge localBridge;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (declareWarButton != null)
            declareWarButton.onClick.AddListener(OnDeclareWarClicked);
    }

    private void Start()
    {
        // Grab the local player's bridge via InstanceFinder
        if (InstanceFinder.ClientManager != null &&
            InstanceFinder.ClientManager.Connection != null &&
            InstanceFinder.ClientManager.Connection.FirstObject != null)
        {
            localBridge = InstanceFinder.ClientManager.Connection.FirstObject.GetComponent<PlayerFactionBridge>();
        }
        else
        {
            Debug.LogWarning("[ClientFactionUI] Could not find local PlayerFactionBridge.");
        }
    }

    /// <summary>
    /// Called by FactionManager.TargetOpenFactionManagement.
    /// Only leaders get this UI.
    /// </summary>
    public void Populate(int factionId, string factionName, bool leader,
        List<ulong> members, List<ulong> invited, List<FactionManager.PlayerListInfo> onlinePlayers)
    {
        currentFactionId = factionId;
        isLeader = leader;

        if (!isLeader)
        {
            Debug.LogWarning("[ClientFactionUI] Populate called but not leader.");
            return;
        }

        if (factionNameText != null)
            factionNameText.text = $"Faction: {factionName} (ID {factionId})";

        // Clear old entries
        foreach (Transform t in contentParent)
            Destroy(t.gameObject);

        Debug.Log("Destroyed content children");
        Debug.Log("Number of online players: " + onlinePlayers.Count);

        // Populate scroll list
        foreach (var p in onlinePlayers)
        {
            Debug.Log("Placing entry for player: " + p.playerName);
            GameObject go = Instantiate(entryPrefab, contentParent);
            PlayerListEntryUI entry = go.GetComponent<PlayerListEntryUI>();
            if (entry != null)
            {
                bool alreadyInvited = invited.Contains(p.clientId);
                entry.Setup(p.clientId, p.playerName, alreadyInvited, OnInviteClicked);
                Debug.Log("Setup player with name " + p.playerName);
            }
        }

        // === WAR DECLARATION DROPDOWN ===
        if (warTargetDropdown != null)
        {
            warTargetDropdown.ClearOptions();
            currentDropdownFactionIds.Clear();

            List<string> options = new List<string>();
            var allFactions = FactionManager.Instance.GetAllFactions();

            foreach (var kv in allFactions)
            {
                int fid = kv.Key;
                if (fid == factionId) continue; // skip your own faction
                options.Add($"{kv.Value.FactionName} (ID {fid})");
                currentDropdownFactionIds.Add(fid);
            }

            warTargetDropdown.AddOptions(options);

            if (declareWarButton != null)
                declareWarButton.interactable = currentDropdownFactionIds.Count > 0;
        }

        if (panel != null)
            panel.SetActive(true);

        Debug.Log($"[ClientFactionUI] Opened for faction {factionId}, {onlinePlayers.Count} players to invite.");
    }

    private void OnInviteClicked(ulong targetClientId)
    {
        if (!isLeader || currentFactionId < 0)
        {
            Debug.LogWarning("[ClientFactionUI] Invalid invite attempt.");
            return;
        }

        Debug.Log($"[ClientFactionUI] Inviting {targetClientId} to faction {currentFactionId}");
        if (localBridge != null)
        {
            localBridge.Invite(currentFactionId, targetClientId);
            Debug.Log("Inviting player...");
        }
        else
        {
            Debug.LogWarning("[ClientFactionUI] No local PlayerFactionBridge found.");
        }
    }

    private void OnLeaveClicked()
    {
        if (currentFactionId < 0) return;

        Debug.Log($"[ClientFactionUI] Leaving faction {currentFactionId}");
        FactionManager.Instance.LeaveFactionServerRpc(currentFactionId);
        Close();
    }

    private void OnDeclareWarClicked()
    {
        if (!isLeader)
        {
            Debug.LogWarning("[ClientFactionUI] Only leaders can declare wars.");
            return;
        }

        if (warTargetDropdown == null || currentDropdownFactionIds.Count == 0)
        {
            Debug.LogWarning("[ClientFactionUI] No factions available to declare war on.");
            return;
        }

        int selectedIndex = warTargetDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= currentDropdownFactionIds.Count) return;

        int defenderFactionId = currentDropdownFactionIds[selectedIndex];
        int attackerFactionId = currentFactionId;

        Debug.Log($"[ClientFactionUI] Declaring war: {attackerFactionId} vs {defenderFactionId}");
        FactionManager.Instance.DeclareWarServerRpc(attackerFactionId, defenderFactionId);
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);

        currentFactionId = -1;
    }

    // Observer hooks (optional UI refreshes)
    public void OnFactionLeft() => Close();
    public void OnMemberAdded(int factionId, ulong newMemberId, string newMemberName) { }
    public void OnMemberRemoved(int factionId, ulong removedClient) { }
    public void OnInviteDeclined(int factionId, ulong declinedClient) { }
}
