// PlayerListEntryUI.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerListEntryUI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public Button inviteButton;
    public TextMeshProUGUI inviteButtonText;

    private ulong clientId;
    private Action<ulong> onInvite;

    public void Setup(ulong clientId, string playerName, bool alreadyInvited, Action<ulong> onInvite)
    {
        this.clientId = clientId;
        this.onInvite = onInvite;
        if (nameText != null) nameText.text = playerName;
        if (inviteButtonText != null) inviteButtonText.text = alreadyInvited ? "Invited" : "Invite";
        inviteButton.interactable = !alreadyInvited;

        inviteButton.onClick.RemoveAllListeners();
        inviteButton.onClick.AddListener(() => this.onInvite?.Invoke(this.clientId));
    }
}
