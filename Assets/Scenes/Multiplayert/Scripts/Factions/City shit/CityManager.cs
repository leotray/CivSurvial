
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using UnityEngine;
using FishNet.Object.Synchronizing;
using FishNet;
using System.Collections;

public class CityManager : NetworkBehaviour
{
    private class ActiveSiege
    {
        public int CityId;
        public int AttackingFactionId;

        public float CaptureProgress;
        public float TimeRemaining;

        public Coroutine Routine;
    }

    public static CityManager Instance;
    [SerializeField] private GameObject cityBlockBlueprintPrefab;
    private readonly Dictionary<int, CityData> _cities = new Dictionary<int, CityData>();
    private readonly Dictionary<int, ActiveSiege> _activeSieges = new();
    private int _nextCityId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryGetCity(int cityId, out CityData city) => _cities.TryGetValue(cityId, out city);

    [Server]
    public int CreateCity(string name, int owningFactionId, Vector3 pos)
    {
        int id = _nextCityId++;
        var city = new CityData(id, name, owningFactionId, pos);
        _cities.Add(id, city);

        Debug.Log($"[CityManager] Created city {name} (id={id}) for faction {owningFactionId} at {pos}");
        return id;
    }

    [Server]
    public void AddCitizen(int cityId, ulong playerId)
    {
        if (_cities.TryGetValue(cityId, out var city))
        {
            if (!city.Citizens.Contains(playerId))
            {
                city.Citizens.Add(playerId);
                Debug.Log($"[CityManager] Player {playerId} added to city {city.CityName}");
            }
        }
    }

    [Server]
    public void MarkCityHallBuilt(int cityId)
    {
        if (_cities.TryGetValue(cityId, out var city))
        {
            city.CityHallBuilt = true;
            Debug.Log($"[CityManager] City hall built in {city.CityName}");
        }
    }
    [Server]
    public void SpawnCityConstructionSite(ulong ownerId, string cityName)
    {
        Debug.Log($"[CityManager] Spawning city site for {ownerId} -> {cityName}");

        if (!NetworkManager || !NetworkManager.IsServer)
        {
            Debug.LogError("[CityManager] Not running on server! Cannot spawn city site.");
            return;
        }

        if (cityBlockBlueprintPrefab == null)
        {
            Debug.LogError("[CityManager] CityBlockBlueprintPrefab not assigned!");
            return;
        }

        if (!ServerManager.Clients.TryGetValue((int)ownerId, out NetworkConnection conn))
        {
            Debug.LogError($"[CityManager] Could not find connection for ownerId {ownerId}.");
            return;
        }

        NetworkObject playerObj = conn.FirstObject;
        if (playerObj == null)
        {
            Debug.LogError($"[CityManager] Could not find player object for ownerId {ownerId}.");
            return;
        }

        Vector3 spawnPos = playerObj.transform.position + playerObj.transform.forward * 20f;
        if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 100f))
            spawnPos = hit.point;

        var instance = Instantiate(cityBlockBlueprintPrefab, spawnPos, Quaternion.identity);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[CityManager] Prefab missing NetworkObject component!");
            return;
        }

        ServerManager.Spawn(netObj);
        Debug.Log($"[CityManager] City construction site spawned successfully at {spawnPos}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSpawnCityConstructionSiteServerRpc(ulong ownerId, string cityName, NetworkConnection sender = null)
    {
        Debug.Log($"[CityManager] Received RequestSpawnCityConstructionSiteServerRpc from {sender?.ClientId ?? 999} for {cityName}");
        SpawnCityConstructionSite(ownerId, cityName);
    }

    [Server]
    public void SetCityLeader(int cityId, ulong newLeaderId)
    {
        if (_cities.TryGetValue(cityId, out var city))
        {
            city.LeaderId = newLeaderId;
            Debug.Log($"[CityManager] Assigned player {newLeaderId} as leader of city {city.CityName}");
        }
        else
        {
            Debug.LogWarning($"[CityManager] Tried to assign leader to invalid cityId {cityId}");
        }
    }

    [Server]
    public bool IsCityLeader(int cityId, ulong playerId)
    {
        return _cities.TryGetValue(cityId, out var city) && city.LeaderId == playerId;
    }
    public void UpdateCity(CityData updatedCity)
    {
        if (_cities.ContainsKey(updatedCity.CityId))
        {
            _cities[updatedCity.CityId] = updatedCity;
            Debug.Log($"[CityManager] Updated city {updatedCity.CityName} ownership -> Faction {updatedCity.OwningFactionId}");
        }
    }
    public bool IsPlayerInCity(ulong playerId)
    {
        foreach (var city in _cities.Values)
        {
            // Check if player is the city leader
            if (city.LeaderId == playerId)
                return true;

            // Check if player is a citizen
            if (city.Citizens != null && city.Citizens.Contains(playerId))
                return true;
        }

        return false;
    }

    public int GetPlayerCityId(ulong playerId)
    {
        foreach (var city in _cities.Values)
        {
            if (city.LeaderId == playerId || city.Citizens.Contains(playerId))
                return city.CityId;
        }
        return -1; // not in a city
    }

    public int GetPlayerFactionId(ulong playerId)
    {
        foreach (var city in _cities.Values)
        {
            if (city.LeaderId == playerId || city.Citizens.Contains(playerId))
                return city.OwningFactionId;
        }
        return -1; // not in a faction
    }

    [Server]
    public void StartCityCapture(int cityId, int attackerFactionId )
    {
        if (_activeSieges.ContainsKey(cityId))
            return;

        if (!_cities.TryGetValue(cityId, out var city))
            return;

        var siege = new ActiveSiege
        {
            CityId = cityId,
            AttackingFactionId = attackerFactionId,
            CaptureProgress = 0f,
            TimeRemaining = 300f // TOTAL siege duration (5 minutes)
        };

        siege.Routine = StartCoroutine(SiegeRoutine(siege));
        _activeSieges.Add(cityId, siege);

        Debug.Log($"[Siege] Siege started on {city.CityName}");
    }

    private IEnumerator SiegeRoutine(ActiveSiege siege)
    {
        float debugInterval = 1f; // log once per second
        float debugTimer = 0f;

        float captureRequired = 120f; // size of capture bar
        float radius = 20f;
        float captureSpeed = 1f;

        CityData city = _cities[siege.CityId];

        while (siege.TimeRemaining > 0f)
        {
            siege.TimeRemaining -= Time.deltaTime;
            debugTimer += Time.deltaTime;

            int attackers = 0;
            int defenders = 0;

            foreach (var conn in ServerManager.Clients.Values)
            {
                if (conn.FirstObject == null)
                    continue;

                Transform player = conn.FirstObject.transform;
                float dist = Vector3.Distance(player.position, city.Position);

                if (dist > radius)
                    continue;

                int playerId = conn.ClientId; // 👈 use int consistently
                int factionId = GetPlayerFactionId((ulong)playerId);

                if (factionId == siege.AttackingFactionId)
                    attackers++;
                else if (factionId == city.OwningFactionId)
                    defenders++;
            }

            int netControl = attackers - defenders;

            if (netControl != 0)
            {
                siege.CaptureProgress += netControl * captureSpeed * Time.deltaTime;
            }

            siege.CaptureProgress = Mathf.Clamp(
                siege.CaptureProgress,
                0f,
                captureRequired
            );

            // 🔎 DEBUG LOG (rate limited)
            if (debugTimer >= debugInterval)
            {
                debugTimer = 0f;

                Debug.Log(
                    $"[Siege DEBUG] City: {city.CityName} | " +
                    $"Attackers: {attackers} | Defenders: {defenders} | " +
                    $"Net: {netControl} | " +
                    $"Progress: {siege.CaptureProgress:F1}/{captureRequired} | " +
                    $"Time Left: {siege.TimeRemaining:F1}s"
                );
            }

            // 🏳️ ATTACKERS WIN
            if (siege.CaptureProgress >= captureRequired)
            {
                Debug.Log("[Siege DEBUG] Capture bar FULL → attackers win");
                CompleteSiege(siege);
                yield break;
            }

            yield return null;
        }

        // TIME RAN OUT → DEFENDERS WIN
        FailSiege(siege);
    }
    [Server]
    private void CompleteSiege(ActiveSiege siege)
    {
        if (!_cities.TryGetValue(siege.CityId, out var city))
            return;

        city.OwningFactionId = siege.AttackingFactionId;
        UpdateCity(city);

        StopCoroutine(siege.Routine);
        _activeSieges.Remove(siege.CityId);

        Debug.Log($"[Siege] {city.CityName} captured by faction {siege.AttackingFactionId}");
    }
    [Server]
    private void FailSiege(ActiveSiege siege)
    {
        StopCoroutine(siege.Routine);
        _activeSieges.Remove(siege.CityId);

        Debug.Log($"[Siege] Siege FAILED on city {siege.CityId}. Attackers forced to retreat.");

        // Later:
        // - teleport attackers out
        // - apply cooldown
        // - morale penalties
    }


}
