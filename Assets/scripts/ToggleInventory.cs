using UnityEngine;

public class ToggleInventory : MonoBehaviour
{
    public GameObject Inventory;
    public GameObject cameraWithMovement; // 👈 Assign your camera in the Inspector

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            bool isNowOpen = !Inventory.activeSelf;

            // Only open if no other UI is open
            if (!isNowOpen || !UIStateManager.Instance.IsUIOpen)
            {
                Inventory.SetActive(isNowOpen);
                UIStateManager.Instance.SetUIOpen(isNowOpen);
                cameraWithMovement.GetComponent<movement>().enabled = !isNowOpen;
            }
        }
    }

}
