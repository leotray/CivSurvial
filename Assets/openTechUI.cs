using UnityEngine;
using FishNet.Object;

public class openTechUI : NetworkBehaviour
{
    private FactionTechUI techUI;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            // find FactionTechUI inside the player's canvas
            techUI = GetComponentInChildren<FactionTechUI>(true);
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // N key toggles panel
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (techUI != null && techUI.IsOpen())
            {
                // close locally
                techUI.Close();
            }
            else
            {
                int factionId = FactionManager.Instance.GetMyFactionId();
                if (factionId >= 0)
                {
                    FactionManager.Instance.RequestOpenFactionTechServerRpc(factionId);
                }
                else
                {
                    Debug.Log("sry twin cant open the tech tree you aint a part of a faction");
                }
            }
        }

        // Esc always closes if open
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (techUI != null && techUI.IsOpen())
            {
                techUI.Close();
            }
        }
    }
}
