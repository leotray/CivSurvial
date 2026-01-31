// FactionManagementUI.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FactionManagementUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Transform contentParent; // content of scroll view
    public GameObject entryPrefab; // prefab that has PlayerListEntryUI
    public TextMeshProUGUI titleText;
    [Header("War UI")]
    public TMP_Dropdown warTargetDropdown;
    public Button declareWarButton;

    private int currentFactionId = -1;
    private List<int> currentDropdownFactionIds = new List<int>();

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }
    private void Start()
    {
        if (declareWarButton != null)
            declareWarButton.onClick.AddListener(OnDeclareWarClicked);
    }

    public void Populate(int factionId, string factionName, List<ulong> members, List<ulong> invited, List<FactionManager.PlayerListInfo> onlinePlayers)
    {
        currentFactionId = factionId;
        if (titleText != null) titleText.text = $"{factionName} (ID {factionId})";

        // clear
        foreach (Transform t in contentParent) Destroy(t.gameObject);

        foreach (var p in onlinePlayers)
        {
            GameObject go = Instantiate(entryPrefab, contentParent);
            PlayerListEntryUI entry = go.GetComponent<PlayerListEntryUI>();
            if (entry != null)
            {
                bool alreadyInvited = invited.Contains(p.clientId);
                entry.Setup(p.clientId, p.playerName, alreadyInvited, OnInviteClicked);
            }
        }

        panel.SetActive(true);
        Debug.Log($"[FactionManagementUI] Opened for faction {factionId} showing {onlinePlayers.Count} players");

        // Populate war target dropdown (other factions)
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

            // If options empty, disable declare button
            if (declareWarButton != null)
                declareWarButton.interactable = currentDropdownFactionIds.Count > 0;
        }


    }

    private void OnInviteClicked(ulong targetClientId)
    {
        if (currentFactionId < 0) { Debug.LogWarning("No faction selected."); return; }
        Debug.Log($"[FactionManagementUI] Inviting {targetClientId} to faction {currentFactionId}");
        FactionManager.Instance.InvitePlayerServerRpc(currentFactionId, targetClientId);
    }

    public void Close()
    {
        panel.SetActive(false);
        currentFactionId = -1;
    }
    private void OnDeclareWarClicked()
    {
        if (warTargetDropdown == null || currentDropdownFactionIds.Count == 0)
        {
            Debug.LogWarning("No factions to declare war on.");
            return;
        }

        int selectedIndex = warTargetDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= currentDropdownFactionIds.Count) return;

        int defenderFactionId = currentDropdownFactionIds[selectedIndex];
        int attackerFactionId = currentFactionId;

        Debug.Log($"[UI] Declaring war: {attackerFactionId} vs {defenderFactionId}");
        FactionManager.Instance.DeclareWarServerRpc(attackerFactionId, defenderFactionId);
    }


}
