// FactionManager.cs (UPDATED)
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using UnityEngine;
using FishNet.Object.Synchronizing;
using FishNet;

public class FactionManager : NetworkBehaviour
{
    private readonly SyncDictionary<int, Faction> _factions = new SyncDictionary<int, Faction>();
    private int _nextFactionId = 1;

    public static FactionManager Instance;

    [Header("Faction Building Prefabs")]
    [SerializeField] private GameObject cityBlockBlueprintPrefab;
    [SerializeField] private GameObject cityCenterPrefab;

    [Header("Tech / Research")]
    [Tooltip("ScriptableObject that contains all techs in the game (client & server should have the same DB)")]
    public TechDatabase techDatabase;

    [Tooltip("How many research points a newly created faction starts with")]
    public int initialTechPoints = 5;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _factions.OnChange += OnFactionListChanged;
        Debug.Log("[FactionManager] Server started and listening.");
    }

    private void OnFactionListChanged(SyncDictionaryOperation op, int key, Faction faction, bool asServer)
    {
        Debug.Log($"[FactionManager] Faction change op:{op} id:{key} name:{faction.FactionName}");
    }

    // ---------------- core create / read ----------------
    [ServerRpc(RequireOwnership = false)]
    public void CreateFactionServer(string factionName, int capitalCityId, NetworkConnection sender = null)
    {
        ulong ownerId = (ulong)sender.ClientId;

        if (GetPlayerFaction(ownerId, out _))
        {
            Debug.LogWarning($"[FactionManager] CreateFaction: player {ownerId} already in faction");
            return;
        }

        int newId = _nextFactionId++;
        Faction f = new Faction(factionName, ownerId, initialTechPoints);
        f.CapitalCityId = capitalCityId;
        _factions.Add(newId, f);

        string playerName = sender.FirstObject.GetComponent<PlayerIdentity>()?.GetPlayerName() ?? $"Player {ownerId}";
        Debug.Log($"[FactionManager] Faction '{factionName}' created by {playerName} with ID {newId}");

        // Update city ownership
        if (CityManager.Instance.TryGetCity(capitalCityId, out var city))
        {
            city.OwningFactionId = newId;
            city.LeaderId = ownerId;

            Debug.Log($"[FactionManager] City '{city.CityName}' now belongs to faction '{factionName}'.");

            // Optional: if City has a method to sync data, call it here
            // city.SyncOwnershipToClients();
        }
        else
        {
            Debug.LogWarning($"[FactionManager] Could not find city with ID {capitalCityId} to set as capital!");
        }

        // Optional feedback to player
        TargetFactionCreated(sender, factionName, capitalCityId);
    }

    [TargetRpc]
    private void TargetFactionCreated(NetworkConnection target, string factionName, int capitalCityId)
    {
        Debug.Log($"[FactionManager] You have successfully founded faction '{factionName}' with city ID {capitalCityId} as your capital.");
    }


    private void SpawnFactionConstructionSite(ulong ownerId, int factionId, string factionName)
    {
        if (cityBlockBlueprintPrefab == null)
        {
            Debug.LogError("[FactionManager] CityBlockBlueprintPrefab not assigned!");
            return;
        }
        if (!ServerManager.Clients.TryGetValue((int)ownerId, out NetworkConnection conn))
        {
            Debug.LogError($"[FactionManager] Could not find connection for ownerId {ownerId}.");
            return;
        }

        NetworkObject playerObj = conn.FirstObject;
        if (playerObj == null)
        {
            Debug.LogError($"[FactionManager] Could not find player object for ownerId {ownerId}.");
            return;
        }

        Transform playerTransform = playerObj.transform;
        Vector3 spawnBase = playerTransform.position + playerTransform.forward * 20f;
        Vector3 rayStart = spawnBase + Vector3.up * 10f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f))
        {
            spawnBase = hit.point;
        }

        GameObject blueprintInstance = Instantiate(cityBlockBlueprintPrefab, spawnBase, Quaternion.identity);
        ServerManager.Spawn(blueprintInstance);
        Debug.Log($"[FactionManager] Faction '{factionName}' created with a construction site at {spawnBase} (factionId={factionId}).");
    }

    public bool GetPlayerFaction(ulong clientId, out int factionId)
    {
        foreach (var kv in _factions)
        {
            if (kv.Value.Members.Contains(clientId)) { factionId = kv.Key; return true; }
        }
        factionId = -1; return false;
    }

    public bool TryGetFaction(int factionId, out Faction faction) => _factions.TryGetValue(factionId, out faction);

    // ------------- invite flow ---------------

    [ServerRpc(RequireOwnership = false)]
    public void RequestFactionManagementServerRpc(int factionId, NetworkConnection sender = null)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] RequestMgmt: faction {factionId} not found");
            return;
        }

        ulong caller = (ulong)sender.ClientId;
        if (caller != faction.OwnerClientId)
        {
            Debug.LogWarning($"[FactionManager] RequestMgmt: {caller} not owner");
            return;
        }

        // build online players list (no filtering now)
        List<PlayerListInfo> online = new List<PlayerListInfo>();
        foreach (var kv in ServerManager.Clients)
        {
            ulong cid = (ulong)kv.Key;
            string name = GetPlayerNameFromServer(cid);
            online.Add(new PlayerListInfo { clientId = cid, playerName = name });
        }

        TargetOpenFactionManagement(sender, factionId, faction.FactionName,
            faction.Members.ToArray(), faction.Invited.ToArray(), online.ToArray());
        Debug.Log($"[FactionManager] Sent management data to leader {caller} for faction {factionId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void InvitePlayerServerRpc(int factionId, ulong targetClientId, NetworkConnection sender = null)
    {
        Debug.Log("factionmanager is sending invite");
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] Invite: faction {factionId} not found");
            return;
        }

        ulong inviter = (ulong)sender.ClientId;
        if (inviter != faction.OwnerClientId)
        {
            Debug.LogWarning($"[FactionManager] Invite: {inviter} not owner");
            return;
        }

        if (faction.Members.Contains(targetClientId))
        {
            Debug.Log($"[FactionManager] Invite: {targetClientId} already member");
            return;
        }
        if (faction.Invited.Contains(targetClientId))
        {
            Debug.Log($"[FactionManager] Invite: {targetClientId} already invited");
            return;
        }

        faction.Invited.Add(targetClientId);
        _factions[factionId] = faction;

        Debug.Log($"[FactionManager] {inviter} invited {targetClientId} to faction {factionId}");

        if (ServerManager.Clients.TryGetValue((int)targetClientId, out NetworkConnection targConn))
        {
            string inviterName = GetPlayerNameFromServer(inviter);
            TargetReceiveInvite(targConn, factionId, faction.FactionName, inviter, inviterName);
            Debug.Log($"[FactionManager] Target RPC sent to {targetClientId}");
        }
        else
        {
            Debug.LogWarning($"[FactionManager] Invite: target {targetClientId} not connected; invite stored in data.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AcceptInviteServerRpc(int factionId, NetworkConnection sender = null)
    {
        ulong client = (ulong)sender.ClientId;
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] AcceptInvite: faction {factionId} not found");
            return;
        }

        if (!faction.Invited.Contains(client))
        {
            Debug.LogWarning($"[FactionManager] AcceptInvite: player {client} not invited");
            return;
        }

        faction.Invited.Remove(client);
        faction.Members.Add(client);
        _factions[factionId] = faction;

        string name = GetPlayerNameFromServer(client);
        Debug.Log($"[FactionManager] Player {client} ({name}) joined faction {factionId}");

        ObserversFactionMemberAdded(factionId, client, name);

        if (ServerManager.Clients.TryGetValue((int)client, out NetworkConnection conn))
            TargetNotifyJoinSuccess(conn, factionId, faction.FactionName);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DeclineInviteServerRpc(int factionId, NetworkConnection sender = null)
    {
        ulong client = (ulong)sender.ClientId;
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] DeclineInvite: faction {factionId} not found");
            return;
        }

        if (!faction.Invited.Contains(client))
        {
            Debug.LogWarning($"[FactionManager] DeclineInvite: player {client} not invited");
            return;
        }

        faction.Invited.Remove(client);
        _factions[factionId] = faction;

        Debug.Log($"[FactionManager] Player {client} declined invite to faction {factionId}");
        ObserversFactionInviteDeclined(factionId, client);
    }

    [ServerRpc(RequireOwnership = false)]
    public void LeaveFactionServerRpc(int factionId, NetworkConnection sender = null)
    {
        ulong client = (ulong)sender.ClientId;
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] LeaveFaction: faction {factionId} not found");
            return;
        }

        if (!faction.Members.Contains(client))
        {
            Debug.LogWarning($"[FactionManager] LeaveFaction: player {client} not a member of {factionId}");
            return;
        }

        faction.Members.Remove(client);
        _factions[factionId] = faction;

        Debug.Log($"[FactionManager] Player {client} left faction {factionId}");

        if (ServerManager.Clients.TryGetValue((int)client, out NetworkConnection conn))
            TargetNotifyLeaveSuccess(conn, factionId);

        ObserversFactionMemberRemoved(factionId, client);
    }

    // ---- server -> client RPCs ----

    [TargetRpc]
    private void TargetOpenFactionManagement(NetworkConnection target, int factionId, string factionName, ulong[] members, ulong[] invited, PlayerListInfo[] onlinePlayers)
    {
        Debug.Log($"[FactionManager->Client] OpenFactionManagement target for faction {factionId}");

        if (target != null && target.FirstObject != null)
        {
            var localPlayerGO = target.FirstObject.gameObject;
            var clientUI = localPlayerGO.GetComponentInChildren<ClientFactionUI>(true);
            if (clientUI != null)
            {
                ulong localClientId = (ulong)target.ClientId;
                bool isLeader = members != null && members.Length > 0 && _factions.TryGetValue(factionId, out Faction fa) && fa.OwnerClientId == localClientId;
                clientUI.Populate(factionId, factionName, isLeader,
                    members.ToList(), invited.ToList(), onlinePlayers.ToList());
                return;
            }
        }

        Debug.LogWarning("[FactionManager] TargetOpenFactionManagement: couldn't find local ClientFactionUI on target; trying InstanceFinder fallback.");
        var fallbackObj = InstanceFinder.ClientManager?.Connection?.FirstObject;
        if (fallbackObj != null)
        {
            var clientUI = fallbackObj.GetComponentInChildren<ClientFactionUI>(true);
            ulong localClientId = (ulong)InstanceFinder.ClientManager.Connection.ClientId;
            bool isLeaderFallback = members != null && members.Length > 0 &&
                                    _factions.TryGetValue(factionId, out Faction fa2) &&
                                    fa2.OwnerClientId == localClientId;

            clientUI.Populate(factionId, factionName, isLeaderFallback,
                members.ToList(), invited.ToList(), onlinePlayers.ToList());
        }
    }

    [TargetRpc]
    private void TargetReceiveInvite(NetworkConnection target, int factionId, string factionName, ulong inviterId, string inviterName)
    {
        Debug.Log($"[FactionManager->Client] Received invite rpc for faction {factionId} from {inviterName}");

        if (target != null && target.FirstObject != null)
        {
            var localPlayerGO = target.FirstObject.gameObject;
            var inviteUI = localPlayerGO.GetComponentInChildren<InviteUI>(true);
            if (inviteUI != null)
            {
                inviteUI.ShowInvite(factionId, factionName, inviterId, inviterName);
                return;
            }
        }

        Debug.LogError("[FactionManager] TargetReceiveInvite: couldn't find InviteUI on target player's prefab!");
    }

    [TargetRpc]
    private void TargetNotifyJoinSuccess(NetworkConnection target, int factionId, string factionName)
    {
        Debug.Log($"[FactionManager->Client] NotifyJoinSuccess for faction {factionId}");

        if (target != null && target.FirstObject != null)
        {
            var inviteUI = target.FirstObject.gameObject.GetComponentInChildren<InviteUI>(true);
            inviteUI?.OnJoinAccepted(factionId, factionName);
        }
    }

    [TargetRpc]
    private void TargetNotifyLeaveSuccess(NetworkConnection target, int factionId)
    {
        Debug.Log($"[FactionManager->Client] NotifyLeaveSuccess for faction {factionId}");

        if (target != null && target.FirstObject != null)
        {
            var clientUI = target.FirstObject.gameObject.GetComponentInChildren<ClientFactionUI>(true);
            clientUI?.OnFactionLeft();
        }
    }

    [ObserversRpc]
    private void ObserversFactionMemberAdded(int factionId, ulong newMemberId, string newMemberName)
    {
        Debug.Log($"[FactionManager->All] MemberAdded: {newMemberName} to {factionId}");

        var localPlayer = InstanceFinder.ClientManager?.Connection?.FirstObject;
        if (localPlayer != null)
        {
            var clientUI = localPlayer.GetComponentInChildren<ClientFactionUI>(true);
            clientUI?.OnMemberAdded(factionId, newMemberId, newMemberName);
        }
    }

    [ObserversRpc]
    private void ObserversFactionMemberRemoved(int factionId, ulong removedClient)
    {
        Debug.Log($"[FactionManager->All] MemberRemoved: {removedClient} from {factionId}");

        var localPlayer = InstanceFinder.ClientManager?.Connection?.FirstObject;
        if (localPlayer != null)
        {
            var clientUI = localPlayer.GetComponentInChildren<ClientFactionUI>(true);
            clientUI?.OnMemberRemoved(factionId, removedClient);
        }
    }

    [ObserversRpc]
    private void ObserversFactionInviteDeclined(int factionId, ulong declinedClient)
    {
        Debug.Log($"[FactionManager->All] InviteDeclined: {declinedClient} for {factionId}");

        var localPlayer = InstanceFinder.ClientManager?.Connection?.FirstObject;
        if (localPlayer != null)
        {
            var clientUI = localPlayer.GetComponentInChildren<ClientFactionUI>(true);
            clientUI?.OnInviteDeclined(factionId, declinedClient);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TransferFactionOwnerServerRpc(int factionId, ulong newOwnerClientId, NetworkConnection sender = null)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction)) return;
        ulong caller = (ulong)sender.ClientId;
        if (caller != faction.OwnerClientId) { Debug.LogWarning("[FactionManager] TransferOwner: caller not owner"); return; }
        faction.OwnerClientId = newOwnerClientId;
        _factions[factionId] = faction;
        Debug.Log($"[FactionManager] Faction {factionId} owner changed to {newOwnerClientId}");
    }

    [Server]
    public void SetFactionLeader(int factionId, ulong newOwnerClientId)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] SetLeader: faction {factionId} not found.");
            return;
        }

        faction.OwnerClientId = newOwnerClientId;
        _factions[factionId] = faction;

        Debug.Log($"[FactionManager] Leader of faction {factionId} set to {newOwnerClientId}");
    }

    private string GetPlayerNameFromServer(ulong clientId)
    {
        if (ServerManager.Clients.TryGetValue((int)clientId, out NetworkConnection conn))
        {
            var identity = conn.FirstObject?.GetComponent<PlayerIdentity>();
            if (identity != null) return identity.GetPlayerName();
        }
        return $"Player {clientId}";
    }

    public int GetMyFactionId()
    {
        var clientConn = InstanceFinder.ClientManager?.Connection;
        if (clientConn == null) return -1;

        ulong myId = (ulong)clientConn.ClientId;

        if (GetPlayerFaction(myId, out int factionId))
            return factionId;

        return -1;
    }

    public struct PlayerListInfo { public ulong clientId; public string playerName; }

    // ============================
    // Tech / Research RPCs below
    // ============================

    /// <summary>
    /// Client requests the server to open the faction tech tree UI for them.
    /// The server will verify ownership and send back the current tech state (including known recipes).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenFactionTechServerRpc(int factionId, NetworkConnection sender = null)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] RequestOpenTech: faction {factionId} not found");
            return;
        }

        ulong caller = (ulong)sender.ClientId;
        // Any faction member can open UI, but only owner can purchase. We will include `isLeader` flag.
        bool isLeader = caller == faction.OwnerClientId;

        // Compose arrays to send
        string[] allTechIds = techDatabase?.GetAllTechIds() ?? new string[0];
        string[] unlockedIds = faction.UnlockedTechIds?.ToArray() ?? new string[0];
        string[] knownRecipes = faction.KnownRecipes?.ToArray() ?? new string[0];
        int techPoints = faction.TechPoints;

        TargetOpenFactionTechTree(sender, factionId, allTechIds, unlockedIds, knownRecipes, techPoints, isLeader);
    }

    /// <summary>
    /// Server: leader requests to unlock a tech for the faction (pays faction tech points).
    /// This updates the faction record, adds recipes and broadcasts to members.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UnlockTechServerRpc(int factionId, string techId, NetworkConnection sender = null)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction))
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: faction {factionId} not found");
            return;
        }

        ulong caller = (ulong)sender.ClientId;
        if (caller != faction.OwnerClientId)
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: caller {caller} not faction owner");
            return;
        }

        if (string.IsNullOrEmpty(techId))
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: invalid techId");
            return;
        }

        if (faction.UnlockedTechIds.Contains(techId))
        {
            Debug.Log($"[FactionManager] UnlockTech: tech {techId} already unlocked");
            return;
        }

        if (techDatabase == null)
        {
            Debug.LogError("[FactionManager] UnlockTech: techDatabase is not assigned on server.");
            return;
        }

        MPtech tech = techDatabase.GetById(techId);
        if (tech == null)
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: tech id {techId} not found in database.");
            return;
        }

        if (faction.TechPoints < tech.cost)
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: not enough faction tech points. Needed {tech.cost}, have {faction.TechPoints}");
            return;
        }
        if (tech.prerequisites != null && tech.prerequisites.Count > 0)
{
    foreach (var prereq in tech.prerequisites)
    {
        if (prereq == null) continue;
        if (!faction.UnlockedTechIds.Contains(prereq.techId))
        {
            Debug.LogWarning($"[FactionManager] UnlockTech: missing prerequisite {prereq.techName} for {tech.techName}");
            return; // fail early
        }
    }
}

        // Subtract faction points and mark unlocked
        faction.TechPoints -= tech.cost;
        faction.UnlockedTechIds.Add(techId);

        // Add unlocked recipes to faction.KnownRecipes
        foreach (var recipeName in tech.unlockedRecipeNames)
        {
            if (string.IsNullOrEmpty(recipeName)) continue;
            if (!faction.KnownRecipes.Contains(recipeName))
                faction.KnownRecipes.Add(recipeName);
        }

        // persist back to syncdictionary
        _factions[factionId] = faction;

        Debug.Log($"[FactionManager] Faction '{faction.FactionName}' unlocked tech '{tech.techName}' and gained {tech.unlockedRecipeNames.Count} recipes. Remaining points: {faction.TechPoints}");

        // Notify all online members: update their recipe lists and reopen/refresh tech UI (if open)
        foreach (ulong member in faction.Members)
        {
            if (ServerManager.Clients.TryGetValue((int)member, out NetworkConnection conn))
            {
                bool isMemberLeader = member == faction.OwnerClientId;
                // send known recipes + refreshed tech UI state to each online member
                TargetSyncRecipes(conn, faction.KnownRecipes.ToArray());
                TargetOpenFactionTechTree(conn, factionId, techDatabase.GetAllTechIds(), faction.UnlockedTechIds.ToArray(),
                    faction.KnownRecipes.ToArray(), faction.TechPoints, isMemberLeader);
            }
        }
    }

    /// <summary>
    /// Player asks the server to send them the faction's known recipes (useful on join / faction change).
    /// Called from PlayerFactionData when the local player's faction id changes.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestSyncKnownRecipesServerRpc(int factionId, NetworkConnection sender = null)
    {
        if (!_factions.TryGetValue(factionId, out Faction faction))
            return;

        TargetSyncRecipes(sender, faction.KnownRecipes.ToArray());
    }

    // --- TargetRPCs sent to clients ---

    [TargetRpc]
    private void TargetSyncRecipes(NetworkConnection target, string[] recipeNames)
    {
        if (target == null || target.FirstObject == null)
        {
            Debug.LogWarning("[FactionManager] TargetSyncRecipes: no valid target object.");
            return;
        }

        var localPlayerObj = target.FirstObject.gameObject;
        var crafting = localPlayerObj.GetComponent<MultiplayerCraftingManager>();
        if (crafting != null)
        {
            crafting.ClientUnlockFactionRecipes(recipeNames);

            Debug.Log($"[FactionManager->Client] Synced {recipeNames.Length} recipes to {localPlayerObj.name}.");
        }
        else
        {
            Debug.LogWarning("[FactionManager] TargetSyncRecipes: MultiplayerCraftingManager not found on local player prefab.");
        }
    }


    [TargetRpc]
    private void TargetOpenFactionTechTree(NetworkConnection target, int factionId, string[] allTechIds, string[] unlockedTechIds, string[] knownRecipeNames, int techPoints, bool isLeader)
    {
        // Runs on client. Finds the local player's FactionTechUI and populates it.
        var localPlayerObj = InstanceFinder.ClientManager?.Connection?.FirstObject;
        if (localPlayerObj != null)
        {
            var ui = localPlayerObj.GetComponentInChildren<FactionTechUI>(true);
            if (ui != null)
            {
                ui.Populate(factionId, allTechIds, unlockedTechIds, knownRecipeNames, techPoints, isLeader);
                Debug.Log("populate with following parameters" + factionId + allTechIds + unlockedTechIds + knownRecipeNames + techPoints + isLeader); ;
                return;
            }
        }

        Debug.LogWarning("[FactionManager] TargetOpenFactionTechTree: local FactionTechUI not found on client.");
    }
    public List<string> GetFactionUnlocked(int factionId)
    {
        if (_factions.TryGetValue(factionId, out var f))
            return f.UnlockedTechIds;
        return new List<string>();
    }
    public int GetFactionIdForPlayer(PlayerRole player)
    {
        ulong clientId = (ulong)player.Owner.ClientId; // Or however you get the clientId from your PlayerRole
        if (GetPlayerFaction(clientId, out int factionId))
            return factionId;
        return -1;
    }
    [ServerRpc(RequireOwnership = false)]
    public void ClaimChunkServerRpc(Vector3 worldPos, NetworkConnection sender = null)
    {
        ulong callerId = (ulong)sender.ClientId;

        if (!GetPlayerFaction(callerId, out int factionId))
        {
            Debug.LogWarning("[FactionManager] ClaimChunk: caller not in faction");
            return;
        }

        if (!_factions.TryGetValue(factionId, out Faction faction))
            return;

        // ✅ Only leader can claim
        if (callerId != faction.OwnerClientId)
        {
            Debug.LogWarning("[FactionManager] ClaimChunk: only leader can claim");
            return;
        }

        Vector2Int coords = LandChunkManager.Instance.GetChunkCoords(worldPos);

        // ✅ Check adjacency: must touch existing claimed chunk
        bool adjacent = faction.ClaimedChunks.Exists(c =>
            (Mathf.Abs(c.x - coords.x) + Mathf.Abs(c.y - coords.y)) == 1);

        if (!adjacent && faction.ClaimedChunks.Count > 0)
        {
            Debug.LogWarning("[FactionManager] ClaimChunk: not adjacent to owned land");
            return;
        }

        if (LandChunkManager.Instance.ClaimChunk(coords, factionId))
        {
            faction.ClaimedChunks.Add(coords);
            _factions[factionId] = faction; // update syncdict
            Debug.Log($"[FactionManager] Faction {factionId} claimed new chunk {coords}");
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void RequestJoinFactionServerRpc(int cityId, int targetFactionId, NetworkConnection sender = null)
    {
        ulong requesterId = (ulong)sender.ClientId;

        if (!CityManager.Instance.TryGetCity(cityId, out var city))
        {
            Debug.LogWarning($"[FactionManager] City {cityId} not found.");
            return;
        }

        if (city.LeaderId != requesterId)
        {
            Debug.LogWarning($"[FactionManager] Player {requesterId} tried to join faction for city {cityId} but isn’t leader.");
            return;
        }

        if (!_factions.TryGetValue(targetFactionId, out var faction))
        {
            Debug.LogWarning($"[FactionManager] Faction {targetFactionId} not found.");
            return;
        }

        // Update the city’s owning faction
        city.OwningFactionId = targetFactionId;
        CityManager.Instance.UpdateCity(city);

        // Optionally add the city’s leader to faction if not already
        if (!faction.Members.Contains(requesterId))
        {
            faction.Members.Add(requesterId);
        }

        _factions[targetFactionId] = faction;

        Debug.Log($"[FactionManager] City '{city.CityName}' has joined faction '{faction.FactionName}'.");

        // Notify leader & city players
        TargetNotifyCityJoinedFaction(sender, city.CityName, faction.FactionName);
    }
    [TargetRpc]
    private void TargetNotifyCityJoinedFaction(NetworkConnection target, string cityName, string factionName)
    {
        Debug.Log($"[FactionManager->Client] Your city '{cityName}' has joined faction '{factionName}'.");
        // optional UI popup here later
    }
    public List<string> GetAllFactionNames()
    {
        return _factions.Values.Select(f => f.FactionName).ToList();
    }

    public int GetFactionIdByIndex(int index)
    {
        if (index < 0 || index >= _factions.Count) return -1;
        return _factions.Keys.ElementAt(index);
    }
    [ServerRpc(RequireOwnership = false)]
public void DeclareWarServerRpc(int attackerFactionId, int defenderFactionId, NetworkConnection sender = null)
{
    if (!InstanceFinder.IsServer) return;

    // Validate factions exist
    if (!_factions.ContainsKey(attackerFactionId) || !_factions.ContainsKey(defenderFactionId))
    {
        Debug.LogWarning($"[FactionManager] DeclareWar: Invalid factions {attackerFactionId} vs {defenderFactionId}");
        return;
    }

    // Validate caller
    ulong callerId = (ulong)sender.ClientId;
    if (!_factions.TryGetValue(attackerFactionId, out Faction attackerFaction))
    {
        Debug.LogWarning("[FactionManager] DeclareWar: attacker faction not found");
        return;
    }

    if (callerId != attackerFaction.OwnerClientId)
    {
        Debug.LogWarning($"[FactionManager] DeclareWar: caller {callerId} is not leader of {attackerFactionId}");
        return;
    }

    // Prevent self-war
    if (attackerFactionId == defenderFactionId)
    {
        Debug.LogWarning("[FactionManager] DeclareWar: cannot declare war on self");
        return;
    }

    // Check if already at war
    if (WarManager.Instance.IsAtWar(attackerFactionId, defenderFactionId))
    {
        Debug.Log($"[FactionManager] Factions {attackerFactionId} and {defenderFactionId} are already at war.");
        return;
    }

    // ✅ Declare war
    WarManager.Instance.DeclareWar(attackerFactionId, defenderFactionId);

    Debug.Log($"[FactionManager] {attackerFaction.FactionName} declared war on {_factions[defenderFactionId].FactionName}");
}
    // Return a shallow dictionary copy of active factions (id -> Faction)
    public Dictionary<int, Faction> GetAllFactions()
    {
        // SyncDictionary supports IEnumerable of kv pairs; create a plain Dictionary copy for callers
        return _factions.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    // Return a list of faction ids in the same order the dictionary exposes them
    public List<int> GetAllFactionIds()
    {
        return _factions.Keys.ToList();
    }


}
