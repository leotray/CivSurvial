using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TradeUIManager : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TradeSlotUI[] mySlots;
    [SerializeField] private TradeSlotUI[] partnerSlots;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text partnerReadyText;
    [SerializeField] private Text partnerMoneyText;
    [SerializeField] private InputField myMoneyInput;   // 👈 drag your input field here
    [SerializeField] private Text myMoneyText;          // 👈 optional text showing your own offer

    private ulong partnerId;
    private bool isReady;
    private PlayerLook playerLook;

    private void Awake()
    {
        panel.SetActive(false);
        readyButton.onClick.AddListener(OnReadyClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        myMoneyInput.onValueChanged.AddListener(OnMoneyChanged);

        playerLook = FindObjectOfType<PlayerLook>();
    }

    public void OpenTrade(ulong partner)
    {
        partnerId = partner;
        isReady = false;
        partnerReadyText.text = "Partner: Not Ready";
        partnerMoneyText.text = "Money: 0";

        myMoneyInput.text = "0";
        if (myMoneyText != null) myMoneyText.text = "Money: 0";

        ClearAllSlots();
        panel.SetActive(true);
        LockForUI(true);
    }

    public void CloseTrade()
    {
        ClearAllSlots();
        panel.SetActive(false);
        LockForUI(false);
    }

    public void UpdatePartnerOffer(ulong who, List<InventoryItemData> items, int money, bool ready)
    {
        if (who == partnerId)
        {
            for (int i = 0; i < partnerSlots.Length; i++)
            {
                if (i < items.Count) partnerSlots[i].Set(items[i]);
                else partnerSlots[i].Clear();
            }

            partnerMoneyText.text = $"Money: {money}";
            partnerReadyText.text = ready ? "Partner: Ready" : "Partner: Not Ready";
        }
    }

    private void OnReadyClicked()
    {
        isReady = !isReady;
        SendOfferToServer();
    }

    private void OnCancelClicked()
    {
        CloseTrade();
    }

    private void OnMoneyChanged(string value)
    {
        if (myMoneyText != null)
        {
            int val = 0;
            int.TryParse(value, out val);
            myMoneyText.text = $"Money: {val}";
        }

        // refresh offer immediately if not ready yet
        if (!isReady) SendOfferToServer();
    }

    private void SendOfferToServer()
    {
        List<InventoryItemData> items = new List<InventoryItemData>();
        foreach (var slot in mySlots)
        {
            if (slot.itemData.HasValue) items.Add(slot.itemData.Value);
        }

        int money = 0;
        int.TryParse(myMoneyInput.text, out money);

        TradeManager.Instance.SubmitTradeOffer(items, money, isReady);
        Debug.Log($"[TradeUIManager] Sent offer to server: {items.Count} item types, money {money}, ready={isReady}");
    }

    private void ClearAllSlots()
    {
        foreach (var s in mySlots) s.Clear();
        foreach (var s in partnerSlots) s.Clear();
    }

    private void LockForUI(bool open)
    {
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (playerLook != null)
            playerLook.enabled = !open;
    }

    // Called when the local player changed their trade panel (drag/drop)
    public void RefreshMyOffer()
    {
        if (!isReady) // only if not locked in
        {
            List<InventoryItemData> items = new List<InventoryItemData>();
            foreach (var slot in mySlots)
            {
                if (slot.itemData.HasValue)
                    items.Add(slot.itemData.Value);
            }

            int money = 0;
            int.TryParse(myMoneyInput.text, out money);

            TradeManager.Instance.SubmitTradeOffer(items, money, isReady);
            Debug.Log("[TradeUIManager] RefreshMyOffer -> submitted updated offer to server.");
        }
        else
        {
            Debug.Log("[TradeUIManager] RefreshMyOffer skipped because already ready.");
        }
    }
}
