using UnityEngine;

public class CraftingTable : MonoBehaviour
{
    public float interactRange = 3f;
    public Transform player;
    public GameObject craftingUI;
    public CraftingManager craftingManager;
    public Camera playerCamera;

    bool uiOpen = false;

    void Awake()
    {
        if (craftingUI == null)
        {
            CraftingUI[] allUIs = Resources.FindObjectsOfTypeAll<CraftingUI>();
            foreach (var ui in allUIs)
            {
                if (ui.isCraftingTableUI)
                {
                    craftingUI = ui.gameObject;
                    break;
                }
            }

            if (craftingUI == null)
                Debug.LogWarning("❌ Could not find crafting table UI (inactive).");
        }

        if (craftingManager == null)
            craftingManager = FindObjectOfType<CraftingManager>();

        if (player == null)
            player = FindObjectOfType<PlayerMovement>()?.transform;

        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>();
    }

    void Start()
    {
        if (craftingUI != null)
            craftingUI.SetActive(false);
    }

    void Update()
    {
        if (player == null || playerCamera == null)
            return;

        Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactRange, Color.cyan);

        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    ToggleUI();
                }
            }
        }
    }

    void ToggleUI()
    {
        bool isNowOpen = !uiOpen;

        // Prevent opening if another UI is already open
        if (isNowOpen && UIStateManager.Instance.IsUIOpen)
            return;

        uiOpen = isNowOpen;
        craftingUI.SetActive(uiOpen);
        UIStateManager.Instance.SetUIOpen(uiOpen);

        if (player != null)
            player.GetComponent<PlayerMovement>().enabled = !uiOpen;

        if (playerCamera != null)
            playerCamera.GetComponent<movement>().enabled = !uiOpen;

        if (uiOpen)
        {
            craftingManager.SetRecipeContainer(craftingManager.recipeContainer);
            craftingUI.GetComponent<CraftingUI>()?.RefreshUI();
        }
        else
        {
            craftingManager.SetRecipeContainer(craftingManager.defaultPlayerRecipeContainer);
            foreach (var ui in FindObjectsOfType<CraftingUI>())
            {
                if (!ui.isCraftingTableUI)
                    ui.RefreshUI();
            }
        }
    }
}
