using UnityEngine;

public class ToggleTechTree : MonoBehaviour
{
    public GameObject TechTreeUI;
    public GameObject cameraWithMovement; // 👈 Assign your camera

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            bool isNowOpen = !TechTreeUI.activeSelf;

            if (!isNowOpen || !UIStateManager.Instance.IsUIOpen)
            {
                TechTreeUI.SetActive(isNowOpen);
                UIStateManager.Instance.SetUIOpen(isNowOpen);
                cameraWithMovement.GetComponent<movement>().enabled = !isNowOpen;
            }
        }
    }

}
