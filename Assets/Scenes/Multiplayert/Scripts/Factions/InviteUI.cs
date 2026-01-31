using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InviteUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public TextMeshProUGUI messageText;
    public Button acceptButton;
    public Button declineButton;

    private int pendingFactionId = -1;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        acceptButton.onClick.AddListener(OnAccept);
        declineButton.onClick.AddListener(OnDecline);
    }

    public void ShowInvite(int factionId, string factionName, ulong inviterId, string inviterName)
    {
        pendingFactionId = factionId;

        if (messageText != null)
            messageText.text = $"{inviterName} invites you to join '{factionName}'";

        if (panel != null) panel.SetActive(true);

        Debug.Log($"[InviteUI] Showing invite from {inviterName} for faction {factionId}");
    }

    private void OnAccept()
    {
        if (pendingFactionId < 0) return;

        // Tell FactionManager to send accept to server
        FactionManager.Instance.AcceptInviteServerRpc(pendingFactionId);

        Close();
    }

    private void OnDecline()
    {
        if (pendingFactionId < 0) return;

        // Tell FactionManager to send decline to server
        FactionManager.Instance.DeclineInviteServerRpc(pendingFactionId);

        Close();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        pendingFactionId = -1;
    }

    public void OnJoinAccepted(int factionId, string factionName)
    {
        Debug.Log($"[InviteUI] Join accepted into {factionName} ({factionId})");
        // optional: show toast or refresh faction UI
    }
}
