using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class Banner : NetworkBehaviour
{
    public int CityId { get; private set; }
    public int FactionId => _factionId;
    private int _factionId;
    private ulong _lastAttackerPlayerId = 0;
    public void RegisterAttacker(ulong attackerPlayerId)
    {
        _lastAttackerPlayerId = attackerPlayerId;
    }
    [Server]
    public void Initialize(int cityId)
    {
        CityId = cityId;
        if (CityManager.Instance.TryGetCity(cityId, out var city))
        {
            _factionId = city.OwningFactionId;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenCityMenuServerRpc(ulong requesterId, NetworkConnection conn = null)
    {
        if (!CityManager.Instance.TryGetCity(CityId, out var city))
        {
            Debug.LogWarning($"[Banner] Invalid CityId {CityId}");
            return;
        }

        if (city.LeaderId != requesterId)
        {
            Debug.LogWarning($"[Banner] Player {requesterId} tried to open city menu for {city.CityName} but is not leader.");
            return;
        }

        TargetOpenCityMenu(conn, CityId, city.CityName);
    }

    [TargetRpc]
    private void TargetOpenCityMenu(NetworkConnection target, int cityId, string cityName)
    {
        var ui = FindObjectOfType<CityManagementUI>(true);
        if (ui != null)
        {
            ui.Open(cityId, cityName);
        }
        else
        {
            Debug.LogWarning("[Banner] No CityManagementUI found in scene.");
        }
    }
    [Server]

    public void OnDestroyed()
    {
        if (_lastAttackerPlayerId == 0)
        {
            Debug.LogWarning("[Banner] Destroyed with no attacker recorded.");
            return;
        }

        int attackerFactionId =
            CityManager.Instance.GetPlayerFactionId(_lastAttackerPlayerId);

        if (attackerFactionId <= 0)
        {
            Debug.LogWarning("[Banner] Attacker has no faction, siege aborted.");
            return;
        }

        Debug.Log(
            $"[Banner] Banner for city {CityId} destroyed by player {_lastAttackerPlayerId} (Faction {attackerFactionId})"
        );

        CityManager.Instance.StartCityCapture(
            CityId,
            attackerFactionId
        );
    }

}
