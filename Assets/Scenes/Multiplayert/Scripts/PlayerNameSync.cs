using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;

namespace CivSurvival
{
    public class PlayerNameSync : NetworkBehaviour
    {
        // v4 uses SyncVar<T> instead of [SyncVar]
        private readonly SyncVar<string> _playerName = new SyncVar<string>("Player");

        [SerializeField] private Text nameText; // UI text above player

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Subscribe to name changes
            _playerName.OnChange += OnNameChanged;

            // Apply current name on start
            OnNameChanged(_playerName.Value, _playerName.Value, false);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _playerName.OnChange -= OnNameChanged;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerNameServer(string newName)
        {
            string finalName = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;
            _playerName.Value = finalName;
        }

        // Signature: oldValue, newValue, asServer
        private void OnNameChanged(string oldName, string newName, bool asServer)
        {
            if (nameText != null)
                nameText.text = newName;

            gameObject.name = $"Player ({newName})";
        }
    }
}
