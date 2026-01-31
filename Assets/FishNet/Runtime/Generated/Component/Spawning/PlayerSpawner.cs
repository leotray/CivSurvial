using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System;
using UnityEngine;

namespace FishNet.Component.Spawning
{
    /// <summary>
    /// Spawns a player object for clients when they connect.
    /// </summary>
    [AddComponentMenu("FishNet/Component/PlayerSpawner")]
    public class PlayerSpawner : MonoBehaviour
    {
        public event Action<NetworkObject> OnSpawned;

        [Tooltip("Prefab to spawn for the player.")]
        [SerializeField]
        private NetworkObject _playerPrefab;

        public void SetPlayerPrefab(NetworkObject nob) => _playerPrefab = nob;

        [Tooltip("True to add player to the active scene when no global scenes are specified.")]
        [SerializeField]
        private bool _addToDefaultScene = true;

        [Tooltip("Spawn points for players.")]
        public Transform[] Spawns = new Transform[0];

        private NetworkManager _networkManager;
        private int _nextSpawn;

        private void Awake()
        {
            InitializeOnce();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
        }

        private void InitializeOnce()
        {
            _networkManager = GetComponentInParent<NetworkManager>() ?? InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogWarning($"[PlayerSpawner] NetworkManager not found for {gameObject.name}.");
                return;
            }
            // Listen for when clients finish loading their start scenes
            _networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
        }

        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer)
                return;

            // Ensure the prefab is valid
            if (_playerPrefab == null)
            {
                Debug.LogWarning($"[PlayerSpawner] Player prefab is null. Cannot spawn for connection {conn.ClientId}.");
                return;
            }
            if (!_playerPrefab.GetComponent<NetworkObject>())
            {
                Debug.LogError("[PlayerSpawner] Player prefab is missing a NetworkObject component!");
                return;
            }

            // Determine spawn position/rotation
            Vector3 position;
            Quaternion rotation;
            SetSpawn(_playerPrefab.transform, out position, out rotation);

            // Instantiate (or get from pool) the player object
            NetworkObject nob = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            if (nob == null)
            {
                Debug.LogError("[PlayerSpawner] Failed to instantiate player prefab.");
                return;
            }

            // Spawn with ownership assigned to this connection
            _networkManager.ServerManager.Spawn(nob, conn);

            // If for some reason ownership was not set, assign it explicitly
            if (nob.Owner != conn)
            {
                nob.GiveOwnership(conn);
                Debug.Log($"[PlayerSpawner] Ownership manually assigned to connection {conn.ClientId}.");
            }
            else
            {
                Debug.Log($"[PlayerSpawner] SUCCESS: Player object spawned and ownership assigned to connection {conn.ClientId}");
            }

            // Add the client to the default scene so it will observe the scene’s objects
            if (_addToDefaultScene)
                _networkManager.SceneManager.AddOwnerToDefaultScene(nob);

            OnSpawned?.Invoke(nob);
        }

        private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            if (Spawns.Length == 0)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
                return;
            }
            Transform result = Spawns[_nextSpawn];
            if (result == null)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
            }
            else
            {
                pos = result.position;
                rot = result.rotation;
            }
            _nextSpawn++;
            if (_nextSpawn >= Spawns.Length)
                _nextSpawn = 0;
        }

        private void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            pos = prefab.position;
            rot = prefab.rotation;
        }
    }
}

