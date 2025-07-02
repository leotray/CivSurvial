using UnityEngine;

public class Anvil : MonoBehaviour
{
    public float interactRange = 3f;
    public Transform player;
    public GameObject anvilUI;
    public CraftingManager craftingManager;
    public Camera playerCamera;
    public Transform anvilRecipeContainer; // 👈 Assign this in the Inspector


    bool uiOpen = false;

    void Awake()
    {
        if (anvilUI == null)
        {
            CraftingUI[] allUIs = Resources.FindObjectsOfTypeAll<CraftingUI>();
            foreach (var ui in allUIs)
            {
                if (ui.isAnvilUI) // ✅ NEW UI flag
                {
                    anvilUI = ui.gameObject;
                    break;
                }
            }

            if (anvilUI == null)
                Debug.LogWarning("❌ Could not find anvil UI (inactive).");
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
        if (anvilUI != null)
            anvilUI.SetActive(false);
    }

    void Update()
    {
        if (player == null || playerCamera == null)
            return;

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

        if (isNowOpen && UIStateManager.Instance.IsUIOpen)
            return;

        uiOpen = isNowOpen;
        anvilUI.SetActive(uiOpen);
        UIStateManager.Instance.SetUIOpen(uiOpen);

        if (player != null)
            player.GetComponent<PlayerMovement>().enabled = !uiOpen;

        if (playerCamera != null)
            playerCamera.GetComponent<movement>().enabled = !uiOpen;

        if (uiOpen)
        {
            craftingManager.SetRecipeContainer(anvilRecipeContainer); // 👈 set the correct container
            anvilUI.GetComponent<CraftingUI>()?.RefreshUI();       // 👈 refresh anvil UI with proper recipes
        }
        else
        {
            craftingManager.SetRecipeContainer(craftingManager.defaultPlayerRecipeContainer);
            foreach (var ui in FindObjectsOfType<CraftingUI>())
            {
                if (!ui.isAnvilUI)
                    ui.RefreshUI();
            }
        }
    }


}
