using UnityEngine;
using FishNet.Object;

public class MPToggleInventory : NetworkBehaviour
{
    public GameObject inventoryUI;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            inventoryUI.SetActive(!inventoryUI.activeSelf);
    }
}
