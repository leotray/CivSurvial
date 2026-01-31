using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System;
using UnityEngine;

namespace FishNet.Component.Spawning
{
    [AddComponentMenu("FishNet/Component/PlayerSpawner")]
    public class PlayerSpawner : MonoBehaviour
    {
        public event Action<NetworkObject> OnSpawned;

        [SerializeField] private NetworkObject _playerPrefab;
        public void SetPlayerPrefab(NetworkObject nob) => _playerPrefab = nob;

        [SerializeField] private bool _addToDefaultScene = true;
        public Transform[] Spawns = new Transform[0];

        private NetworkManager _networkManager;
        private int _nextSpawn;

        private void Awake()
        {
            InitializeOnce();
            Debug.Log($"[PlayerSpawner] Awake on {gameObject.name}. _playerPrefab is {(_playerPrefab != null ? _playerPrefab.name : "null")}.");
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
                Debug.LogWarning($"[PlayerSpawner] InitializeOnce: NetworkManager not found for {gameObject.name}.");
                return;
            }
            _networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            Debug.Log($"[PlayerSpawner] Initialized with NetworkManager {(_networkManager != null ? _networkManager.name : "null")}.");
        }

        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            Debug.Log($"[PlayerSpawner] SceneManager_OnClientLoadedStartScenes: conn {conn.ClientId} | asServer:{asServer}");

            if (!asServer)
            {
                Debug.LogWarning($"[PlayerSpawner] Not server. Skipping spawn for conn {conn.ClientId}.");
                return;
            }
            if (_playerPrefab == null || !_playerPrefab.GetComponent<NetworkObject>())
            {
                Debug.LogWarning("[PlayerSpawner] Invalid player prefab. Skipping spawn.");
                return;
            }

            Vector3 position;
            Quaternion rotation;
            SetSpawn(_playerPrefab.transform, out position, out rotation);

            NetworkObject nob = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            if (nob == null)
            {
                Debug.LogWarning("[PlayerSpawner] Failed to instantiate player prefab (GetPooledInstantiated returned null).");
                return;
            }

            _networkManager.ServerManager.Spawn(nob, conn);
            Debug.Log($"[PlayerSpawner] Spawned player object for conn {conn.ClientId} (ObjName: {nob.name})");

            if (nob.Owner != conn)
            {
                nob.GiveOwnership(conn);
                Debug.Log($"[PlayerSpawner] Ownership given to conn {conn.ClientId}.");
            }

            if (_addToDefaultScene)
            {
                _networkManager.SceneManager.AddOwnerToDefaultScene(nob);
                Debug.Log($"[PlayerSpawner] Added player object to default scene for conn {conn.ClientId}.");
            }

            // Do NOT set names here. Owners send their name via ServerRpc in PlayerIdentity.
            OnSpawned?.Invoke(nob);
        }

        private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            if (Spawns.Length == 0)
            {
                pos = prefab.position;
                rot = prefab.rotation;
                Debug.Log("[PlayerSpawner] No spawn points defined, using prefab position.");
                return;
            }

            Transform result = Spawns[_nextSpawn];
            if (result != null)
            {
                pos = result.position;
                rot = result.rotation;
                Debug.Log($"[PlayerSpawner] Using spawn point {result.name} at index {_nextSpawn}.");
            }
            else
            {
                pos = prefab.position;
                rot = prefab.rotation;
                Debug.Log($"[PlayerSpawner] Spawn point at index {_nextSpawn} is null. Using prefab position.");
            }

            _nextSpawn = (_nextSpawn + 1) % Spawns.Length;
        }
    }
}
