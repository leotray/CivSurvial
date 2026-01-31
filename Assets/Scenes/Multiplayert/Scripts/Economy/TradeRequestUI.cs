using UnityEngine;
using UnityEngine.UI;

public class TradeRequestUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text messageText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;

    private ulong requestingPlayerId;
    private PlayerLook playerLook;

    private void Awake()
    {
        panel.SetActive(false);
        acceptButton.onClick.AddListener(OnAccept);
        declineButton.onClick.AddListener(OnDecline);

        // Find local player look (safe for single-player owned context)
        playerLook = FindObjectOfType<PlayerLook>();
    }

    public void ShowRequest(ulong requesterId, string requesterName)
    {
        requestingPlayerId = requesterId;
        messageText.text = $"{requesterName} wants to trade with you.";
        panel.SetActive(true);

        LockForUI(true);

        Debug.Log($"[TradeRequestUI] Showing trade request from {requesterName} (ID {requesterId})");
    }

    // Called from TradeManager when responder receives response ack (optional)
    public void OnRemoteResponse(ulong responderId, bool accepted)
    {
        Debug.Log($"[TradeRequestUI] Remote response from {responderId}: {(accepted ? "accepted" : "declined")}");
    }

    private void OnAccept()
    {
        panel.SetActive(false);
        LockForUI(false);

        Debug.Log($"[TradeRequestUI] Accepted trade from {requestingPlayerId}");
        var tradeManager = FindObjectOfType<TradeManager>();
        tradeManager?.SendTradeResponse(requestingPlayerId, true);
    }

    private void OnDecline()
    {
        panel.SetActive(false);
        LockForUI(false);

        Debug.Log($"[TradeRequestUI] Declined trade from {requestingPlayerId}");
        var tradeManager = FindObjectOfType<TradeManager>();
        tradeManager?.SendTradeResponse(requestingPlayerId, false);
    }

    private void LockForUI(bool open)
    {
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (playerLook != null)
            playerLook.enabled = !open;
    }
}
