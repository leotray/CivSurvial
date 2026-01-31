using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Text interactText;

    [Header("Menus")]
    [SerializeField] private GameObject playerMenuPanel;
    [SerializeField] private GameObject payMenuPanel;
    [SerializeField] private InputField amountInput;
    [SerializeField] private Button confirmPayButton;
    [SerializeField] private Button cancelPayButton;
    [SerializeField] private Button payButton;   // from PlayerMenu
    [SerializeField] private Button tradeButton; // from PlayerMenu
    [SerializeField] private LayerMask interactableLayer; // <-- assign in inspector (banner layer)

    private Camera playerCamera;
    private PlayerLook playerLook;
    private bool menuOpen = false;
    private PlayerWallet targetWallet;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            playerCamera = Camera.main;
            playerLook = GetComponent<PlayerLook>();

            interactText.gameObject.SetActive(false);
            playerMenuPanel.SetActive(false);
            payMenuPanel.SetActive(false);

            // Hook button events
            payButton.onClick.AddListener(OnPayClicked);
            cancelPayButton.onClick.AddListener(ClosePayMenu);
            confirmPayButton.onClick.AddListener(OnConfirmPay);
            tradeButton.onClick.AddListener(OnTradeClicked);
        }
        else
        {
            interactText.gameObject.SetActive(false);
            playerMenuPanel.SetActive(false);
            payMenuPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (!menuOpen)
            CheckForPlayer();

        if (menuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
            ClosePayMenu();
        }
    }

    private void CheckForPlayer()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, playerLayer))
        {
            PlayerWallet wallet = hit.collider.GetComponent<PlayerWallet>();
            if (wallet != null && hit.collider.gameObject != gameObject)
            {
                interactText.gameObject.SetActive(true);
                interactText.text = "Press B to open Player Menu";

                if (Input.GetKeyDown(KeyCode.B))
                {
                    targetWallet = wallet; // remember who we're interacting with
                    Debug.Log($"[PlayerInteraction] Target wallet set to {wallet.gameObject.name}");
                    OpenMenu();
                }
            }
        }
        else
        {
            interactText.gameObject.SetActive(false);
        }
    }

    private void OpenMenu()
    {
        menuOpen = true;
        playerMenuPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerLook != null)
            playerLook.enabled = false;
    }

    private void CloseMenu()
    {
        menuOpen = false;
        playerMenuPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerLook != null)
            playerLook.enabled = true;
    }

    private void OnPayClicked()
    {
        Debug.Log("[PlayerInteraction] Pay clicked");
        playerMenuPanel.SetActive(false);
        payMenuPanel.SetActive(true);
    }

    private void ClosePayMenu()
    {
        payMenuPanel.SetActive(false);
        CloseMenu();
    }

    private void OnConfirmPay()
    {
        if (targetWallet == null)
        {
            Debug.LogWarning("[PlayerInteraction] No target wallet to pay.");
            return;
        }

        if (int.TryParse(amountInput.text, out int amount))
        {
            Debug.Log($"[PlayerInteraction] Confirming pay of {amount} to {targetWallet.gameObject.name}");
            CmdPay(targetWallet, amount);
        }

        ClosePayMenu();
    }

    [ServerRpc]
    private void CmdPay(PlayerWallet target, int amount)
    {
        PlayerWallet myWallet = GetComponent<PlayerWallet>();
        if (myWallet != null)
        {
            bool success = myWallet.TryPay(target, amount);
            if (!success)
                Debug.LogWarning($"{gameObject.name} failed to pay {amount} to {target.gameObject.name}");
            else
                Debug.Log($"{gameObject.name} paid {amount} to {target.gameObject.name}");
        }
    }

    private void OnTradeClicked()
    {
        if (targetWallet == null)
        {
            Debug.LogWarning("[PlayerInteraction] Tried to trade, but no target wallet selected.");
            return;
        }

        ulong targetId = (ulong)targetWallet.Owner.ClientId;
        Debug.Log($"[PlayerInteraction] Sending trade request to {targetWallet.gameObject.name} (ID {targetId})");
        TradeManager.Instance.RequestTradeServerRpc(targetId);
        CloseMenu();
    }
}
