using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class MPPickUp : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        if (!other.CompareTag("pickable")) return;

        MPIteValue itemValue = other.GetComponentInParent<MPIteValue>();
        if (itemValue == null) return;

        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj == null) return;

        RequestPickupServerRpc(netObj, itemValue.itemGuid, itemValue.amount);
    }

    [ServerRpc]
    private void RequestPickupServerRpc(NetworkObject pickupNetObj, string itemGuid, int amount, NetworkConnection conn = null)
    {
        if (!base.IsServer) return;

        if (pickupNetObj == null || !pickupNetObj.IsSpawned) return;

        MPInventoryManager inv = GetComponent<MPInventoryManager>();
        if (inv == null) return;

        inv.RequestAddItem(itemGuid, amount);

        pickupNetObj.Despawn();
    }
}
