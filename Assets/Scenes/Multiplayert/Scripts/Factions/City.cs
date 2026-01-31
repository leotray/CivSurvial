using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet;
using FishNet.Connection;
using UnityEngine;

public class City : NetworkBehaviour
{
    private Dictionary<ulong, int> contributorCounts = new Dictionary<ulong, int>();
    private ElectionManager electionManager;

    [SerializeField] private bool cityHallBuilt = false;
    public bool CityHallBuilt => cityHallBuilt;
    public int CityId { get; private set; }

    public void Initialize(int cityId)
    {
        CityId = cityId;
    }
    private void Awake()
    {
        electionManager = GetComponent<ElectionManager>();
    }

    [Server]
    public void InitializeContributors(Dictionary<ulong, int> contributors)
    {
        contributorCounts.Clear();
        foreach (var kvp in contributors)
            contributorCounts[kvp.Key] = kvp.Value;

        Debug.Log($"[City] Contributors initialized – {contributorCounts.Count} players.");
    }

    [Server]
    public void MarkCityHallBuilt()
    {
        cityHallBuilt = true;
        Debug.Log("[City] City Hall has been marked as built.");
        CityManager.Instance.MarkCityHallBuilt(CityId);


        if (electionManager != null)
        {
            Debug.Log("[City] Starting election...");
            electionManager.StartElection();
        }
        else
        {
            Debug.LogError("[City] ElectionManager not found!");
        }
    }

    public bool IsContributor(ulong playerId) => contributorCounts.ContainsKey(playerId);

    [ObserversRpc]
    public void RpcOpenElectionUI(List<ulong> contributorIds)
    {
        ulong localId = (ulong)InstanceFinder.ClientManager.Connection.ClientId;
        if (contributorIds.Contains(localId))
            StartCoroutine(WaitForElectionUIThenShow(contributorIds));
    }

    private IEnumerator WaitForElectionUIThenShow(List<ulong> ids)
    {
        ElectionUI ui = null;
        int attempts = 0;

        while (ui == null && attempts < 300)
        {
            ui = FindLocalElectionUI();
            attempts++;
            yield return null;
        }

        if (ui != null)
        {
            ui.StartElection();
            ui.Show(ids);
        }
        else
        {
            Debug.LogError("[City] ERROR: Local ElectionUI not found after waiting!");
        }
    }

    private ElectionUI FindLocalElectionUI()
    {
        var localPlayerObjs = GameObject.FindGameObjectsWithTag("Player");
        foreach (var playerObj in localPlayerObjs)
        {
            var networkObj = playerObj.GetComponent<NetworkObject>();
            if (networkObj != null && networkObj.IsOwner)
                return playerObj.GetComponentInChildren<ElectionUI>(true);
        }
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestReopenElectionUIServer(ulong playerId, NetworkConnection conn = null)
    {
        if (!cityHallBuilt || !IsContributor(playerId))
            return;

        if (conn != null)
            RpcForceShowElectionUI(conn, new List<ulong>(contributorCounts.Keys));
    }

    [TargetRpc]
    private void RpcForceShowElectionUI(NetworkConnection conn, List<ulong> contributors)
    {
        StartCoroutine(WaitForElectionUIThenShow(contributors));
    }

    // Fixed: Method signature updated to receive winner ID and name
    [ObserversRpc]
    public void RpcAnnounceWinnerToContributors(ulong winnerId, string winnerName)
    {
        var localPlayerObjs = GameObject.FindGameObjectsWithTag("Player");
        foreach (var playerObj in localPlayerObjs)
        {
            var networkObj = playerObj.GetComponent<NetworkObject>();
            if (networkObj != null && networkObj.IsOwner)
            {
                var uiManager = playerObj.GetComponentInChildren<ElectionUIManager>(true);
                if (uiManager != null)
                {
                    // Fixed: Use the actual winner's name, not local player's name
                    uiManager.ShowWinnerAnnouncement($"{winnerName.ToUpper()} IS OUR NEW LEADER");
                    Debug.Log($"[City] Announced winner: {winnerName} (Client {winnerId})");
                }
                break; // Only need to find the local player once
            }
        }
    }
    public void OnCityHallBuilt(int factionId)
    {
        cityHallBuilt = true;

        // Get all colliders that the City Hall touches
        Bounds bounds = GetComponent<Collider>().bounds;

        // Get all chunk coords overlapped by these bounds
        Vector2Int min = LandChunkManager.Instance.GetChunkCoords(bounds.min);
        Vector2Int max = LandChunkManager.Instance.GetChunkCoords(bounds.max);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector2Int coords = new Vector2Int(x, y);
                if (LandChunkManager.Instance.ClaimChunk(coords, factionId))
                {
                    if (FactionManager.Instance.TryGetFaction(factionId, out var faction))
                    {
                        faction.ClaimedChunks.Add(coords);
                    }
                }
            }
        }

        Debug.Log($"[City] Faction {factionId} claimed starting city hall chunks from {min} to {max}");
    }


    public IEnumerable<ulong> GetContributors() => contributorCounts.Keys;
}
