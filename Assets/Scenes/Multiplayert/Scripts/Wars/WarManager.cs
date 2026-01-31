using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using UnityEngine;

public class WarManager : NetworkBehaviour
{
    public static WarManager Instance;

    private readonly List<WarData> _activeWars = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ✅ Called by the server when a faction declares war
    [Server]
    public void DeclareWar(int attackerFactionId, int defenderFactionId)
    {
        // Prevent duplicate wars
        if (_activeWars.Any(w => w.Involves(attackerFactionId, defenderFactionId)))
        {
            Debug.LogWarning($"[WarManager] War already exists between {attackerFactionId} and {defenderFactionId}");
            return;
        }

        var newWar = new WarData(attackerFactionId, defenderFactionId);
        _activeWars.Add(newWar);

        Debug.Log($"[WarManager] War declared: Faction {attackerFactionId} vs Faction {defenderFactionId}");

        // Broadcast to clients (optional)
        RpcNotifyNewWar(attackerFactionId, defenderFactionId);
    }

    [ObserversRpc]
    private void RpcNotifyNewWar(int attackerFactionId, int defenderFactionId)
    {
        Debug.Log($"[Client] War started: Faction {attackerFactionId} vs Faction {defenderFactionId}");
    }

    public bool IsAtWar(int factionA, int factionB)
    {
        return _activeWars.Any(w => w.Involves(factionA, factionB));
    }

    public void EndWar(WarData war)
    {
        if (_activeWars.Contains(war))
        {
            _activeWars.Remove(war);
            Debug.Log($"[WarManager] War ended between {war.AttackerFactionId} and {war.DefenderFactionId}");
        }
    }

    public WarData GetWarBetween(int factionA, int factionB)
    {
        return _activeWars.FirstOrDefault(w => w.Involves(factionA, factionB));
    }
}
