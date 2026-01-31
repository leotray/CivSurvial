using UnityEngine;
using FishNet;
using FishNet.Object;

public class FactionTestHelper : NetworkBehaviour
{
    void Update()
    {
        if (!IsOwner)
            return;  // Run only on the owning client

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (FactionManager.Instance == null || CityManager.Instance == null)
            {
                Debug.LogWarning("FactionManager or CityManager instance not found!");
                return;
            }

            // Check if this instance is a connected client
            if (!InstanceFinder.NetworkManager.IsClient)
            {
                Debug.LogWarning("You must be connected to create a city.");
                return;
            }

            ulong myPlayerId = (ulong)Owner.ClientId;

            // ✅ Check if player already owns or is part of a city
            if (CityManager.Instance.IsPlayerInCity(myPlayerId))
            {
                Debug.LogWarning("You already belong to a city! Cannot create another one.");
                return;
            }

            // Proceed to request a city spawn
            CityManager.Instance.RequestSpawnCityConstructionSiteServerRpc(myPlayerId, "New City");
            Debug.Log("City creation requested.");
        }
    }
}
