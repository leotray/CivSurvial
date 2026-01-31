using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class PlayerFactionBridge : NetworkBehaviour
{
    public void Invite(int factionId, ulong targetClientId)
    {
        if (IsOwner)
        {
            FactionManager.Instance.InvitePlayerServerRpc(factionId, targetClientId);
            Debug.Log("os pwmer amd asked this ass script to invite bro");
        }
        else
        {
            Debug.Log("sry gng you arent owner");
        }

    }
}

