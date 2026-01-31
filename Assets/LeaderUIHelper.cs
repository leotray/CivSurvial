using UnityEngine;
using FishNet.Object;
using FishNet;

public class LeaderUIHelper : NetworkBehaviour
{
    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Y)) // press Y to open faction UI
        {
            var fm = FactionManager.Instance;
            if (fm == null)
            {
                Debug.LogWarning("No FactionManager found!");
                return;
            }

            int myFactionId = fm.GetMyFactionId(); // you need a way to query this
            if (myFactionId < 0)
            {
                Debug.Log("You’re not in a faction.");
                return;
            }

            fm.RequestFactionManagementServerRpc(myFactionId);
        }
    }
}
