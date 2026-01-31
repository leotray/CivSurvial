using UnityEngine;
using FishNet.Managing;
using System.Threading.Tasks;

public class GameNetworkManager : MonoBehaviour
{
    private NetworkManager _networkManager;

    [Tooltip("Optional: set this so the UI can show the host's join code")]
    public RelayUI relayUI;

    // timeout for waiting for transport configuration (ms)
    public int TransportConfigWaitMs = 3000;

    private void Awake()
    {
        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager == null)
            Debug.LogError("[GameNetworkManager] NetworkManager not found in scene.");
    }

    private void Start()
    {
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState += (args) =>
                Debug.Log($"[GameNetworkManager] Server state: {args.ConnectionState}");
            _networkManager.ClientManager.OnClientConnectionState += (args) =>
                Debug.Log($"[GameNetworkManager] Client state: {args.ConnectionState}");
        }
    }

    public async void StartHost()
    {
        if (RelayManager.Instance == null)
        {
            Debug.LogError("[GameNetworkManager] RelayManager.Instance is null. Make sure RelayManager GameObject exists and is active.");
            return;
        }

        Debug.Log("[GameNetworkManager] Creating Relay allocation for host...");
        string joinCode = await RelayManager.Instance.CreateRelay(10);

        if (!string.IsNullOrEmpty(joinCode) && RelayManager.Instance.LastConfigurationSuccess)
        {
            Debug.Log("[GameNetworkManager] Relay allocation OK. JoinCode: " + joinCode);

            // small wait for transport to stabilize
            await Task.Delay(200);

            try
            {
                bool serverStarted = _networkManager.ServerManager.StartConnection();
                Debug.Log($"[GameNetworkManager] ServerManager.StartConnection returned: {serverStarted}");
                if (!serverStarted)
                {
                    Debug.LogError("[GameNetworkManager] Server failed to start.");
                    return;
                }

                bool clientStarted = _networkManager.ClientManager.StartConnection();
                Debug.Log($"[GameNetworkManager] ClientManager.StartConnection returned: {clientStarted}");
                if (!clientStarted)
                {
                    Debug.LogError("[GameNetworkManager] Local host client failed to start.");
                }

                relayUI?.ShowJoinCode(joinCode);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GameNetworkManager] Exception while starting host connections: " + ex);
            }
        }
        else
        {
            Debug.LogError("[GameNetworkManager] Failed to create relay allocation or transport configuration failed.");
        }
    }

    public async void StartClient(string joinCode)
    {
        if (RelayManager.Instance == null)
        {
            Debug.LogError("[GameNetworkManager] RelayManager.Instance is null. Make sure RelayManager GameObject exists and is active.");
            return;
        }

        Debug.Log("[GameNetworkManager] Attempting to join Relay with code: " + joinCode);
        var joinAlloc = await RelayManager.Instance.JoinRelay(joinCode);

        if (joinAlloc != null)
        {
            // Wait for transport config success with timeout
            int waited = 0;
            while (!RelayManager.Instance.LastConfigurationSuccess && waited < TransportConfigWaitMs)
            {
                await Task.Delay(100);
                waited += 100;
            }

            if (!RelayManager.Instance.LastConfigurationSuccess)
            {
                Debug.LogError("[GameNetworkManager] Transport configuration for join did not complete in time. Aborting client start.");
                return;
            }

            Debug.Log("[GameNetworkManager] Join allocation OK. Starting client connection...");
            try
            {
                bool clientStarted = _networkManager.ClientManager.StartConnection();
                Debug.Log($"[GameNetworkManager] ClientManager.StartConnection returned: {clientStarted}");
                if (!clientStarted)
                    Debug.LogError("[GameNetworkManager] Client failed to start (StartConnection returned false).");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GameNetworkManager] Exception while starting client connection: " + ex);
            }
        }
        else
        {
            Debug.LogError("[GameNetworkManager] Failed to join relay — JoinAllocation was null.");
        }
    }
}
