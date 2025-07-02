using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleFurnaceUI : MonoBehaviour
{
    public GameObject FurnaceUI;
    public GameObject cameraWithMovement; // 👈 Assign your camera

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            bool isNowOpen = !FurnaceUI.activeSelf;
            FurnaceUI.SetActive(isNowOpen);

            Cursor.lockState = isNowOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isNowOpen;

            cameraWithMovement.GetComponent<movement>().enabled = !isNowOpen;

            if (isNowOpen)
            {
                // 🟢 Refresh the crafting UI when opened
                FindObjectOfType<CraftingManager>().RefreshUI();
            }
        }
    }
}
