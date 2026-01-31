using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class ElectionManager : NetworkBehaviour
{
    [SerializeField] private float electionDuration = 60f;
    private bool electionActive = false;
    private float electionEndTime;

    private Dictionary<ulong, int> voteCounts = new Dictionary<ulong, int>();
    private Dictionary<ulong, ulong> whoVotedFor = new Dictionary<ulong, ulong>();
    private List<ulong> candidates = new List<ulong>();

    private City cityRef;

    private void Awake()
    {
        cityRef = GetComponent<City>();
    }

    [Server]
    public void StartElection()
    {
        if (electionActive || cityRef == null)
            return;

        candidates = new List<ulong>(cityRef.GetContributors());
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[ElectionManager] No candidates found.");
            return;
        }

        voteCounts.Clear();
        whoVotedFor.Clear();
        foreach (ulong cand in candidates)
            voteCounts[cand] = 0;

        electionActive = true;
        electionEndTime = Time.time + electionDuration;

        Debug.Log($"[ElectionManager] Election started with {candidates.Count} candidates.");
        cityRef.RpcOpenElectionUI(candidates);
        StartCoroutine(ElectionTimerRoutine());
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitVoteServerRpc(ulong voterId, ulong candidateId, NetworkConnection conn = null)
    {
        if (!electionActive || !candidates.Contains(candidateId))
            return;

        if (whoVotedFor.TryGetValue(voterId, out ulong oldCandidate))
        {
            if (voteCounts.ContainsKey(oldCandidate))
                voteCounts[oldCandidate]--;
        }

        whoVotedFor[voterId] = candidateId;
        voteCounts[candidateId]++;
        Debug.Log($"[ElectionManager] Voter {voterId} voted for {candidateId}");
    }

    private IEnumerator ElectionTimerRoutine()
    {
        yield return new WaitForSeconds(electionDuration);
        EndElection();
    }

    [Server]
    private void EndElection()
    {
        electionActive = false;

        if (voteCounts.Count == 0)
        {
            Debug.Log("[ElectionManager] No votes cast.");
            return;
        }

        int highestVotes = voteCounts.Values.Max();
        var topCandidates = voteCounts
            .Where(kv => kv.Value == highestVotes)
            .Select(kv => kv.Key)
            .ToList();

        ulong winnerId = topCandidates.Count == 1
            ? topCandidates[0]
            : topCandidates[Random.Range(0, topCandidates.Count)];

        string winnerName = GetPlayerNameFromServer(winnerId);

        Debug.Log($"[ElectionManager] Winner: {winnerName} (Client {winnerId}) with {highestVotes} votes.");

        // ✅ Set the winner as faction leader (if part of a faction)
        if (FactionManager.Instance != null && FactionManager.Instance.GetPlayerFaction(winnerId, out int factionId))
        {
            FactionManager.Instance.SetFactionLeader(factionId, winnerId);
        }

        // ✅ Also assign them as leader of this city
        if (CityManager.Instance != null && cityRef != null)
        {
            CityManager.Instance.SetCityLeader(cityRef.CityId, winnerId);
        }


        // Notify contributors
        cityRef.RpcAnnounceWinnerToContributors(winnerId, winnerName);
    }

    [Server]
    private string GetPlayerNameFromServer(ulong clientId)
    {
        if (ServerManager.Clients.TryGetValue((int)clientId, out NetworkConnection conn))
        {
            PlayerIdentity identity = conn.FirstObject?.GetComponent<PlayerIdentity>();
            if (identity != null)
            {
                string name = identity.GetPlayerName();
                Debug.Log($"[ElectionManager] Found winner name '{name}' for client {clientId}");
                return name;
            }
        }

        Debug.LogWarning($"[ElectionManager] Could not find name for winner client {clientId}");
        return $"Player {clientId}";
    }

    public bool IsElectionActive() => electionActive;
    public List<ulong> GetCandidates() => candidates;
}
