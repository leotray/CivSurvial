// PlayerFactionData.cs (UPDATED)
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerFactionData : NetworkBehaviour
{
    private readonly SyncVar<int> _factionId = new SyncVar<int>(-1);

    public int FactionId
    {
        get => _factionId.Value;
        set
        {
            if (IsServer)
                _factionId.Value = value;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _factionId.OnChange += OnFactionIdChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _factionId.OnChange -= OnFactionIdChanged;
    }

    private void OnFactionIdChanged(int oldId, int newId, bool asServer)
    {
        Debug.Log($"FactionId changed from {oldId} to {newId}");

        if (IsOwner && newId >= 0 && FactionManager.Instance != null)
        {
            // Ask server to send us the recipes for our new faction
            FactionManager.Instance.RequestSyncKnownRecipesServerRpc(newId);
        }
    }

}
