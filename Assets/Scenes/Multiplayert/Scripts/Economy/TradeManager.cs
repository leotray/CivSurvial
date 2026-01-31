using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class TradeManager : NetworkBehaviour
{
    public static TradeManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Active trades (maps each participant id -> session)
    private readonly Dictionary<ulong, TradeSession> activeTrades = new Dictionary<ulong, TradeSession>();

    // -------------------------
    // REQUEST FLOW
    // -------------------------
    [ServerRpc(RequireOwnership = false)]
    public void RequestTradeServerRpc(ulong targetId, NetworkConnection sender = null)
    {
        ulong requesterId = (ulong)sender.ClientId;
        Debug.Log($"[TradeManager] Received trade request {requesterId} → {targetId}");

        string requesterName = GetPlayerName(requesterId);

        if (ServerManager.Clients.TryGetValue((int)targetId, out NetworkConnection targetConn))
        {
            TargetShowTradeRequest(targetConn, requesterId, requesterName);
        }
        else
        {
            Debug.LogWarning($"[TradeManager] Target client {targetId} not found.");
        }
    }

    [TargetRpc]
    private void TargetShowTradeRequest(NetworkConnection target, ulong requesterId, string requesterName)
    {
        var ui = FindObjectOfType<TradeRequestUI>(true);
        if (ui != null)
        {
            ui.ShowRequest(requesterId, requesterName);
        }
        else
            Debug.LogError("[TradeManager] TradeRequestUI not found on target client.");
    }

    // Public call from UI (client-side)
    public void SendTradeResponse(ulong requesterId, bool accepted)
    {
        CmdSendTradeResponse(requesterId, accepted);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdSendTradeResponse(ulong requesterId, bool accepted, NetworkConnection sender = null)
    {
        ulong responderId = (ulong)sender.ClientId;
        Debug.Log($"[TradeManager] Received trade response from {responderId} => {(accepted ? "ACCEPT" : "DECLINE")} for request {requesterId}");

        if (ServerManager.Clients.TryGetValue((int)requesterId, out NetworkConnection requesterConn))
        {
            TargetReceiveTradeResponse(requesterConn, responderId, accepted);
        }

        if (accepted)
        {
            StartTrade(requesterId, responderId);
        }
    }

    [TargetRpc]
    private void TargetReceiveTradeResponse(NetworkConnection requesterConn, ulong responderId, bool accepted)
    {
        var ui = FindObjectOfType<TradeRequestUI>(true);
        if (ui != null) ui.OnRemoteResponse(responderId, accepted);

        if (accepted)
        {
            Debug.Log("[TradeManager] Trade accepted — requester will open Trade UI.");
            FindObjectOfType<TradeUIManager>(true)?.OpenTrade(responderId);
        }
        else
        {
            Debug.Log("[TradeManager] Trade declined.");
        }
    }

    // -------------------------
    // TRADE SESSION
    // -------------------------
    private void StartTrade(ulong p1, ulong p2)
    {
        var session = new TradeSession(p1, p2);
        activeTrades[p1] = session;
        activeTrades[p2] = session;

        if (ServerManager.Clients.TryGetValue((int)p1, out var c1))
            TargetStartTrade(c1, p2);
        if (ServerManager.Clients.TryGetValue((int)p2, out var c2))
            TargetStartTrade(c2, p1);
    }

    [TargetRpc]
    private void TargetStartTrade(NetworkConnection conn, ulong partnerId)
    {
        var ui = FindObjectOfType<TradeUIManager>(true);
        if (ui != null) ui.OpenTrade(partnerId);
    }

    // Client calls this to submit/refresh their offer
    public void SubmitTradeOffer(List<InventoryItemData> items, int money, bool ready)
    {
        CmdSubmitTradeOffer(items, money, ready);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdSubmitTradeOffer(List<InventoryItemData> items, int money, bool ready, NetworkConnection sender = null)
    {
        ulong submitter = (ulong)sender.ClientId;
        if (!activeTrades.TryGetValue(submitter, out TradeSession session))
        {
            Debug.LogWarning("[TradeManager] SubmitTradeOffer from player not in active trade.");
            return;
        }

        var wallet = GetWallet(submitter);
        if (wallet == null)
        {
            Debug.LogWarning("[TradeManager] SubmitTradeOffer failed: no wallet found.");
            return;
        }

        // Validate money
        if (money < 0 || money > wallet.GetBalance())
        {
            Debug.LogWarning($"[TradeManager] Player {submitter} tried to offer invalid money amount: {money}");
            money = 0; // Reset invalid offers
        }

        // Resolve submitter inventory
        MPInventoryManager inv = GetInventory(submitter);
        if (inv == null)
        {
            Debug.LogWarning($"[TradeManager] SubmitTradeOffer failed: no inventory for {submitter}");
            return;
        }

        // previous offer for this submitter (so we can treat available counts correctly)
        List<InventoryItemData> prevOffer = session.GetOfferFor(submitter);

        // Build available counts: server inventory + prevOffer (since prevOffer items are currently removed from inventory)
        Dictionary<string, int> available = new Dictionary<string, int>();
        foreach (var s in inv.inventory)
        {
            if (available.ContainsKey(s.itemGuid)) available[s.itemGuid] += s.count;
            else available[s.itemGuid] = s.count;
        }
        foreach (var p in prevOffer)
        {
            if (available.ContainsKey(p.itemGuid)) available[p.itemGuid] += p.count;
            else available[p.itemGuid] = p.count;
        }

        // Validate that the new items are available
        foreach (var it in items)
        {
            if (!available.ContainsKey(it.itemGuid) || available[it.itemGuid] < it.count)
            {
                Debug.LogWarning($"[TradeManager] Player {submitter} tried to offer {it.count}x {it.itemGuid} but only { (available.ContainsKey(it.itemGuid) ? available[it.itemGuid] : 0) } available (including previous offer).");
                // Do not change session; bail out
                return;
            }
        }

        // Now: refund previous offer back into inventory (server-side)
        if (prevOffer.Count > 0)
        {
            Debug.Log($"[TradeManager] Refunding previous offer for {submitter} ({prevOffer.Count} item types).");
            foreach (var p in prevOffer)
                inv.AddItemServer(p.itemGuid, p.count);
        }

        // Remove new offered items from inventory (server-side)
        foreach (var it in items)
        {
            int removed = inv.RemoveItem(it.itemGuid, it.count);
            if (removed < it.count)
            {
                Debug.LogWarning($"[TradeManager] Unexpected: failed to remove full amount {it.itemGuid} x{it.count} from {submitter}. Removed {removed}. Rolling back.");

                // Rollback: refund any removed amounts and re-apply the previous offer
                // (We re-add the remaining newly-removed counts back.)
                if (removed > 0)
                    inv.AddItemServer(it.itemGuid, removed);

                // Re-apply previous offer (subtract it again, because we refunded earlier)
                foreach (var p in prevOffer)
                    inv.RemoveItem(p.itemGuid, p.count);

                return;
            }
        }

        // Update server session state to new offer (server-owned)
        session.UpdateOffer(submitter, items, money, ready);

        // Notify both players (target RPC to both)
        ulong partner = session.GetPartner(submitter);
        if (ServerManager.Clients.TryGetValue((int)partner, out NetworkConnection partnerConn))
        {
            TargetUpdateTradeUI(partnerConn, submitter, items, money, ready);
        }

        if (ServerManager.Clients.TryGetValue((int)submitter, out NetworkConnection submitterConn))
            TargetUpdateTradeUI(submitterConn, submitter, items, money, ready);

        Debug.Log($"[TradeManager] SubmitTradeOffer accepted for {submitter}. Items removed from their inventory on server.");

        // If both ready, execute
        if (session.BothReady())
            ExecuteTrade(session);
    }

    [TargetRpc]
    private void TargetUpdateTradeUI(NetworkConnection conn, ulong who, List<InventoryItemData> items, int money, bool ready)
    {
        var ui = FindObjectOfType<TradeUIManager>(true);
        if (ui != null) ui.UpdatePartnerOffer(who, items, money, ready);
    }

    private void ExecuteTrade(TradeSession session)
    {
        Debug.Log("[TradeManager] Executing trade between " + session.player1 + " and " + session.player2);

        var inv1 = GetInventory(session.player1);
        var inv2 = GetInventory(session.player2);
        var wallet1 = GetWallet(session.player1);
        var wallet2 = GetWallet(session.player2);

        if (inv1 == null || inv2 == null || wallet1 == null || wallet2 == null)
        {
            Debug.LogWarning("[TradeManager] Missing components for trade execution.");
            CleanupSession(session);
            return;
        }

        // Final check: both players still have enough money
        if (session.offer1.money > wallet1.GetBalance() || session.offer2.money > wallet2.GetBalance())
        {
            Debug.LogWarning("[TradeManager] Trade canceled: insufficient funds.");
            // refund items to owners
            RefundOffers(session);
            CleanupSession(session);
            return;
        }

        // Transfer items: add items from offer1 to inv2, and offer2 to inv1.
        foreach (var it in session.offer1.items)
            inv2.AddItemServer(it.itemGuid, it.count);
        foreach (var it in session.offer2.items)
            inv1.AddItemServer(it.itemGuid, it.count);

        // Transfer money
        wallet1.TryPay(wallet2, session.offer1.money);
        wallet2.TryPay(wallet1, session.offer2.money);

        Debug.Log("[TradeManager] Trade executed successfully.");

        // Clear offers and close UI
        session.ClearOffers();
        CleanupSession(session);
    }

    private void RefundOffers(TradeSession session)
    {
        // Refund both players the items from their offers (if any)
        var inv1 = GetInventory(session.player1);
        var inv2 = GetInventory(session.player2);

        if (inv1 != null)
        {
            foreach (var it in session.offer1.items)
                inv1.AddItemServer(it.itemGuid, it.count);
        }
        if (inv2 != null)
        {
            foreach (var it in session.offer2.items)
                inv2.AddItemServer(it.itemGuid, it.count);
        }
    }

    private void CleanupSession(TradeSession session)
    {
        // If either side still had a non-executed offer, refund it
        if (!session.BothReady())
        {
            Debug.Log("[TradeManager] Cleaning up unexecuted session, refunding offers.");
            RefundOffers(session);
        }

        activeTrades.Remove(session.player1);
        activeTrades.Remove(session.player2);

        if (ServerManager.Clients.TryGetValue((int)session.player1, out var c1))
            TargetCloseTrade(c1);
        if (ServerManager.Clients.TryGetValue((int)session.player2, out var c2))
            TargetCloseTrade(c2);
    }

    [TargetRpc]
    private void TargetCloseTrade(NetworkConnection conn)
    {
        var ui = FindObjectOfType<TradeUIManager>(true);
        if (ui != null) ui.CloseTrade();
    }

    // -------------------------
    // Helpers
    // -------------------------
    private MPInventoryManager GetInventory(ulong id)
    {
        if (ServerManager.Clients.TryGetValue((int)id, out var conn))
            return conn.FirstObject?.GetComponent<MPInventoryManager>();
        return null;
    }

    private PlayerWallet GetWallet(ulong id)
    {
        if (ServerManager.Clients.TryGetValue((int)id, out var conn))
            return conn.FirstObject?.GetComponent<PlayerWallet>();
        return null;
    }

    private string GetPlayerName(ulong clientId)
    {
        if (ServerManager.Clients.TryGetValue((int)clientId, out NetworkConnection conn))
        {
            var identity = conn.FirstObject?.GetComponent<PlayerIdentity>();
            if (identity != null) return identity.GetPlayerName();
        }
        return $"Player {clientId}";
    }
}

// ===== Supporting classes =====
public class TradeSession
{
    public ulong player1, player2;
    public TradeOffer offer1 = new TradeOffer();
    public TradeOffer offer2 = new TradeOffer();

    public TradeSession(ulong p1, ulong p2) { player1 = p1; player2 = p2; }

    public void UpdateOffer(ulong who, List<InventoryItemData> items, int money, bool ready)
    {
        if (who == player1) offer1.Set(items, money, ready);
        else offer2.Set(items, money, ready);
    }

    public List<InventoryItemData> GetOfferFor(ulong who)
    {
        if (who == player1) return new List<InventoryItemData>(offer1.items);
        return new List<InventoryItemData>(offer2.items);
    }

    public bool BothReady() => offer1.ready && offer2.ready;
    public ulong GetPartner(ulong me) => (me == player1) ? player2 : player1;

    public void ClearOffers()
    {
        offer1 = new TradeOffer();
        offer2 = new TradeOffer();
    }
}

[System.Serializable]
public class TradeOffer
{
    public List<InventoryItemData> items = new List<InventoryItemData>();
    public int money = 0;
    public bool ready = false;

    public void Set(List<InventoryItemData> i, int m, bool r)
    {
        items = new List<InventoryItemData>(i);
        money = m;
        ready = r;
    }
}
