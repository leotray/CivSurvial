using UnityEngine;

public class ToggleCraftingUI : MonoBehaviour
{
    public GameObject CraftingUI;
    public GameObject cameraWithMovement; // 👈 Assign your camera

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            bool isNowOpen = !CraftingUI.activeSelf;

            if (!isNowOpen || !UIStateManager.Instance.IsUIOpen)
            {
                CraftingUI.SetActive(isNowOpen);
                UIStateManager.Instance.SetUIOpen(isNowOpen);
                cameraWithMovement.GetComponent<movement>().enabled = !isNowOpen;

                if (isNowOpen)
                    FindObjectOfType<CraftingManager>().RefreshUI();
            }
        }
    }


}
