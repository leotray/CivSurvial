using UnityEngine;
using FishNet.Object;

public class MPToggleCraftingUI : NetworkBehaviour
{
    public GameObject craftingUI;
    public GameObject cameraWithMovement; // Assign your player or camera that has the movement script

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            bool isNowOpen = !craftingUI.activeSelf;

            craftingUI.SetActive(isNowOpen);

            // Optional: sync with global UI state manager if you have one
            if (UIStateManager.Instance != null)
                UIStateManager.Instance.SetUIOpen(isNowOpen);

            if (cameraWithMovement != null)
                cameraWithMovement.GetComponent<PlayerMovementMultiplayer>().enabled = !isNowOpen; // **disable movement when UI is open**

            // Cursor handling
            Cursor.lockState = isNowOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isNowOpen;

            if (isNowOpen)
            {
                // Only refresh UI if it’s your local player opening it
                FindObjectOfType<MultiplayerCraftingUI>()?.RefreshUI();
            }
        }
    }
}
