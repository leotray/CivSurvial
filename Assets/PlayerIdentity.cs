using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerIdentity : NetworkBehaviour
{
    private readonly SyncVar<string> _playerName = new SyncVar<string>("Player");
    [SerializeField] private Text nameText;
    private bool _nameSent = false;

    public string GetPlayerName() => _playerName.Value;

    private void Awake()
    {
        Debug.Log($"[PlayerIdentity] Awake on {gameObject.name}. nameText is '{(nameText != null ? nameText.name : "null")}'.");
        if (nameText == null)
        {
            nameText = GetComponentInChildren<Text>(true);
            if (nameText == null)
                Debug.LogWarning($"[PlayerIdentity] Awake: nameText not assigned or found on {gameObject.name}");
            else
                Debug.Log($"[PlayerIdentity] Awake: nameText found: {nameText.name} on {gameObject.name}");
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        // Fix: Use base.Owner.IsLocalClient instead of IsOwner
        bool isLocalOwner = base.Owner != null && base.Owner.IsLocalClient;
        Debug.Log($"[PlayerIdentity] OnStartNetwork | IsServer:{IsServer} IsClient:{IsClient} IsLocalOwner:{isLocalOwner} | {gameObject.name}");

        // Listen for name changes.
        _playerName.OnChange += OnNameChanged;
        // Immediately apply the current SyncVar value locally (e.g. when a new client spawns).
        ApplyNameLocally(_playerName.Value);

        // Fix: Use base.Owner.IsLocalClient instead of IsOwner in OnStartNetwork
        if (base.Owner != null && base.Owner.IsLocalClient)
        {
            StartCoroutine(SendNameWhenOwner());
            Debug.Log("started coroutine");
        }
        else
        {
            Debug.Log($"[PlayerIdentity] OnStartNetwork on {gameObject.name}: Not owner, not sending name. OwnerId:{(Owner != null ? Owner.ClientId.ToString() : "null")}");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[PlayerIdentity] OnStartClient | IsServer:{IsServer} IsClient:{IsClient} IsOwner:{IsOwner} OwnerId:{(Owner != null ? Owner.ClientId.ToString() : "null")} | {gameObject.name}");

        // Ensure any existing SyncVar value is applied (in case OnNameChanged hasn't fired yet).
        StartCoroutine(ApplyNameNextFrame());
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        // Clean up the event subscription.
        _playerName.OnChange -= OnNameChanged;
    }

    [ServerRpc(RequireOwnership = true)]
    private void ServerSetName(string desiredName)
    {
        // Server side: set the final name (trim whitespace, or default if blank).
        string finalName = string.IsNullOrWhiteSpace(desiredName) ? "Player" : desiredName.Trim();
        if (_playerName.Value == finalName)
        {
            Debug.Log($"[PlayerIdentity] (Server) ServerSetName called with '{desiredName}', finalName '{finalName}', no change needed.");
            return;
        }
        _playerName.Value = finalName;
        Debug.Log($"[PlayerIdentity] (Server) Set name='{finalName}' for {gameObject.name} (Owner:{Owner?.ClientId})");
    }

    private void OnNameChanged(string oldName, string newName, bool asServer)
    {
        Debug.Log($"[PlayerIdentity] OnNameChanged for {gameObject.name}: '{oldName}' -> '{newName}' | asServer:{asServer}");
        ApplyNameLocally(newName);
    }

    private void ApplyNameLocally(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
        else
        {
            Debug.LogWarning($"[PlayerIdentity] ApplyNameLocally: nameText is null on {gameObject.name}");
        }

        // Also update the GameObject's name for easier hierarchy viewing.
        gameObject.name = $"Player ({newName})";
        Debug.Log($"[PlayerIdentity] ApplyNameLocally: Set GameObject.name to 'Player ({newName})' on {gameObject.name}");
    }

    private IEnumerator ApplyNameNextFrame()
    {
        yield return null;
        ApplyNameLocally(_playerName.Value);
    }

    private IEnumerator SendNameWhenOwner()
    {
        if (_nameSent)
        {
            Debug.Log($"[PlayerIdentity] SendNameWhenOwner: Name already sent for {gameObject.name}, skipping.");
            yield break;
        }

        Debug.Log($"[PlayerIdentity] SendNameWhenOwner started for {gameObject.name}. Waiting to become Owner...");
        // Wait until this NetworkBehaviour's object is owned by the local client.
        while (!IsOwner || Owner == null)
        {
            yield return null;
        }
        Debug.Log($"[PlayerIdentity] {gameObject.name} is now owner, sending name to server.");

        // Give one more frame for any other initializations.
        yield return null;

        // Choose the name from the static handler.
        string chosenName = PlayerNameHandler.PlayerName;
        if (string.IsNullOrWhiteSpace(chosenName))
        {
            chosenName = $"Player {Owner.ClientId}";
            Debug.Log($"[PlayerIdentity] No name entered. Using default ID name '{chosenName}'.");
        }

        _nameSent = true;
        ApplyNameLocally(chosenName);
        Debug.Log($"[PlayerIdentity] (Client) Sending name '{chosenName}' to server for {gameObject.name} (Owner:{Owner.ClientId})");
        ServerSetName(chosenName);
    }

    [Server]
    public void SetAndPushNameServerLegacy(string newName)
    {
        // Legacy server-only method to set name directly.
        string finalName = string.IsNullOrWhiteSpace(newName) ? "Player" : newName.Trim();
        _playerName.Value = finalName;
        Debug.Log($"[PlayerIdentity] (Server-Legacy) Set name='{finalName}' for {gameObject.name}");
    }
}
