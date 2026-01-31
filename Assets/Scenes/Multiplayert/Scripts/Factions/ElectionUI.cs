using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Object;
using FishNet.Connection;

public class ElectionUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Transform contentParent;
    public GameObject entryPrefab;

    private PlayerLook localPlayerLook;
    private bool electionActive = false;
    private bool wasShownOnce = false;

    private City localCity;
    private ElectionUIManager uiManager;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (uiManager == null)
            uiManager = GetComponentInChildren<ElectionUIManager>(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
            TryReopenUI();
    }

    public void Show(List<ulong> contributors)
    {
        if (panel == null)
        {
            Debug.LogError("[ElectionUI] Panel not assigned!");
            return;
        }

        if (localPlayerLook == null)
            localPlayerLook = FindLocalPlayerLook();

        if (localPlayerLook != null)
            localPlayerLook.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ClearEntries();

        foreach (var id in contributors)
        {
            GameObject go = Instantiate(entryPrefab, contentParent);
            Text t = go.GetComponentInChildren<Text>();

            // Fixed: Get player name from actual PlayerIdentity objects in scene
            string name = GetPlayerNameFromScene(id);

            if (t != null)
                t.text = name;

            Button voteButton = go.GetComponentInChildren<Button>();
            if (voteButton != null)
            {
                ulong targetId = id;
                voteButton.onClick.AddListener(() => SubmitVote(targetId));
            }
        }

        panel.SetActive(true);
        wasShownOnce = true;
    }

    // Fixed: Method to get player name from scene objects instead of server manager
    private string GetPlayerNameFromScene(ulong clientId)
    {
        // Find all player objects in the scene
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject playerObj in playerObjects)
        {
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null && netObj.Owner != null && (ulong)netObj.Owner.ClientId == clientId)
            {
                PlayerIdentity identity = playerObj.GetComponent<PlayerIdentity>();
                if (identity != null)
                {
                    string playerName = identity.GetPlayerName();
                    Debug.Log($"[ElectionUI] Found player name '{playerName}' for client {clientId}");
                    return playerName;
                }
            }
        }

        Debug.LogWarning($"[ElectionUI] Could not find player name for client {clientId}, using fallback");
        return $"Player {clientId}";
    }

    private void SubmitVote(ulong candidateId)
    {
        ulong voterId = (ulong)InstanceFinder.ClientManager.Connection.ClientId;

        ElectionManager manager = FindObjectOfType<ElectionManager>();
        if (manager != null)
        {
            manager.SubmitVoteServerRpc(voterId, candidateId);
            Debug.Log($"[ElectionUI] Voted for Player {candidateId}");
            Hide();
        }
        else
        {
            Debug.LogWarning("[ElectionUI] ElectionManager not found.");
        }

        uiManager?.HideWinnerAnnouncement();
    }

    private void ClearEntries()
    {
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        if (localPlayerLook != null)
            localPlayerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnCloseButton()
    {
        Hide();
    }

    public void StartElection()
    {
        electionActive = true;
        wasShownOnce = false;
    }

    public void EndElection()
    {
        electionActive = false;
        wasShownOnce = false;
    }

    private void TryReopenUI()
    {
        if (!wasShownOnce && !electionActive)
        {
            Debug.Log("[ElectionUI] Cannot reopen – election not active.");
            return;
        }

        ulong playerId = (ulong)InstanceFinder.ClientManager.Connection.ClientId;

        if (localCity == null)
            localCity = FindLocalCity();

        if (localCity != null)
        {
            localCity.RequestReopenElectionUIServer(playerId);
        }
        else
        {
            Debug.LogWarning("[ElectionUI] No City reference found for reopening UI.");
        }
    }

    private PlayerLook FindLocalPlayerLook()
    {
        foreach (var look in FindObjectsOfType<PlayerLook>())
        {
            if (look.IsOwner)
                return look;
        }
        return null;
    }

    private City FindLocalCity()
    {
        foreach (var city in FindObjectsOfType<City>())
        {
            return city;
        }
        return null;
    }
}
