using UnityEngine;

public class FurnaceInteract : MonoBehaviour
{
    public GameObject furnaceUI;
    public Transform player;
    public GameObject cameraWithMovement;
    public float interactRange = 3f;

    void Awake()
    {
        if (furnaceUI == null)
        {
            FurnaceUI[] allFurnaces = Resources.FindObjectsOfTypeAll<FurnaceUI>();
            foreach (var ui in allFurnaces)
            {
                furnaceUI = ui.gameObject;
                break;
            }

            if (furnaceUI == null)
                Debug.LogWarning("❌ Could not find Furnace UI (even if inactive).");
        }

        if (player == null)
            player = FindObjectOfType<PlayerMovement>()?.transform;

        if (cameraWithMovement == null && player != null)
        {
            Camera foundCam = player.GetComponentInChildren<Camera>();
            if (foundCam != null)
                cameraWithMovement = foundCam.gameObject;
        }
    }

    void Start()
    {
        if (furnaceUI != null)
            furnaceUI.SetActive(false);
    }

    void Update()
    {
        if (player == null || cameraWithMovement == null)
            return;

        Debug.DrawRay(cameraWithMovement.transform.position, cameraWithMovement.transform.forward * interactRange, Color.yellow);

        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = new Ray(cameraWithMovement.transform.position, cameraWithMovement.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                if (hit.transform == transform)
                {
                    ToggleUI();
                }
            }
        }

        if (furnaceUI != null && furnaceUI.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUI();
        }
    }

    void ToggleUI()
    {
        bool isNowOpen = !furnaceUI.activeSelf;

        // Prevent opening if another UI is already open
        if (isNowOpen && UIStateManager.Instance.IsUIOpen)
            return;

        furnaceUI.SetActive(isNowOpen);
        UIStateManager.Instance.SetUIOpen(isNowOpen);

        if (player != null)
            player.GetComponent<PlayerMovement>().enabled = !isNowOpen;

        if (cameraWithMovement != null)
            cameraWithMovement.GetComponent<movement>().enabled = !isNowOpen;
    }

    void CloseUI()
    {
        if (furnaceUI != null)
            furnaceUI.SetActive(false);

        UIStateManager.Instance.SetUIOpen(false);

        if (player != null)
            player.GetComponent<PlayerMovement>().enabled = true;

        if (cameraWithMovement != null)
            cameraWithMovement.GetComponent<movement>().enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
