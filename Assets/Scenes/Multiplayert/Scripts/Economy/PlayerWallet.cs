using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerWallet : NetworkBehaviour
{
    // -----------------------------
    // SyncVar (new v4 style)
    // -----------------------------
    public readonly SyncVar<int> balance = new SyncVar<int>();

    // UI reference (assigned in inspector per player prefab)
    [SerializeField] private Text balanceText;

    // -----------------------------
    // Lifecycle
    // -----------------------------
    public override void OnStartServer()
    {
        base.OnStartServer();
        balance.Value = 0; // new player starts with 0
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // subscribe to balance changes
        balance.OnChange += OnBalanceChanged;

        // update UI immediately
        if (IsOwner)
            UpdateUI(balance.Value);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        balance.OnChange -= OnBalanceChanged;
    }

    // -----------------------------
    // Public API
    // -----------------------------
    [Server]
    public void AddMoney(int amount)
    {
        balance.Value = Mathf.Max(0, balance.Value + amount);
    }

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (balance.Value >= amount)
        {
            balance.Value -= amount;
            return true;
        }
        return false;
    }

    public int GetBalance() => balance.Value;

    // -----------------------------
    // Sync Hook
    // -----------------------------
    private void OnBalanceChanged(int oldValue, int newValue, bool asServer)
    {
        if (IsOwner)
        {
            Debug.Log($"[Wallet] Balance updated: {newValue}");
            UpdateUI(newValue);
        }
    }

    // -----------------------------
    // UI
    // -----------------------------
    private void UpdateUI(int value)
    {
        if (balanceText != null)
            balanceText.text = $"Balance: {value}";
    }

    // -----------------------------
    // Testing Controls
    // -----------------------------
    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Equals))
            CmdChangeBalance(100);

        if (Input.GetKeyDown(KeyCode.Minus))
            CmdChangeBalance(-100);
    }

    [ServerRpc]
    private void CmdChangeBalance(int delta)
    {
        AddMoney(delta);
    }
    [Server]
    public bool TryPay(PlayerWallet target, int amount)
    {
        if (target == null || target == this) return false;
        if (balance.Value < amount || amount <= 0) return false;

        balance.Value -= amount;
        target.AddMoney(amount);
        return true;
    }

}
