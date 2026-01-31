using UnityEngine;
using FishNet.Object;
using FishNet;

public class FactionClaimTester : NetworkBehaviour
{
    void Update()
    {
        // Only let the local player run input
        if (!IsOwner) return;

        // Press C to claim the chunk you're standing in
        if (Input.GetKeyDown(KeyCode.C))
        {
            var fm = FactionManager.Instance;
            if (fm != null)
            {
                fm.ClaimChunkServerRpc(transform.position);
                Debug.Log("[ClaimTester] Requested claim for chunk at " + transform.position);
            }
        }
    }
}
