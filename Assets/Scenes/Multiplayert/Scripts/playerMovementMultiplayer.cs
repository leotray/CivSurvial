using System.Collections;
using UnityEngine;
using FishNet.Object;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementMultiplayer : NetworkBehaviour
{
    public float sprintSpeed = 10f;
    public float exhaustedSpeed = 6f; // slower sprint when stamina is 0
    public float slowSpeed = 2f;
    public float normalSpeed = 5f;
    private float currentSpeed;
    public float cameraYOffset;
    public Transform glasses;
    public Transform cameraTransform;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private Animator animator;

    [Header("Held Item")]
    public Transform heldItemPlaceholder;
    public Transform remoteItemHolder;

    [Header("UI References")]
    public GameObject craftingUI;
    private bool isCraftingOpen = false;

    private PlayerStatsNetworked stats;

    public override void OnStartClient()
    {
        base.OnStartClient();

        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            cam.gameObject.SetActive(IsOwner);
            cameraTransform = cam.transform;
        }

        movement moveScript = GetComponentInChildren<movement>(true);
        if (moveScript != null)
        {
            moveScript.enabled = IsOwner;
        }

        stats = GetComponent<PlayerStatsNetworked>();

        Debug.Log($"[{name}] IsOwner: {IsOwner}, IsClient: {IsClient}, IsServer: {IsServer}");
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleCraftingToggle();

        if (!isCraftingOpen)
        {
            HandleInput();
            HandleAnimation();
        }

        glasses.position = transform.position + new Vector3(0, 1.7f, 0);
        glasses.rotation = cameraTransform.rotation;

        UpdateHeldItemRotation();
    }

    void FixedUpdate()
    {
        if (IsOwner && !isCraftingOpen)
        {
            MovePlayer();
        }
    }

    void HandleInput()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * input.z + camRight * input.x).normalized;

        stats.isMoving = moveDirection.magnitude > 0.1f;

        if (Input.GetKey(KeyCode.LeftShift) && stats.currentStamina > 0)
        {
            stats.isSprinting = true;
            currentSpeed = sprintSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && stats.currentStamina <= 0)
        {
            stats.isSprinting = true;
            currentSpeed = exhaustedSpeed; // tired sprint
        }
        else if (Input.GetKey(KeyCode.LeftAlt))
        {
            stats.isSprinting = false;
            currentSpeed = slowSpeed;
        }
        else
        {
            stats.isSprinting = false;
            currentSpeed = normalSpeed;
        }
    }

    void MovePlayer()
    {
        Vector3 targetPosition = rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }

    void HandleAnimation()
    {
        float animSpeed = moveDirection.magnitude * currentSpeed;
        animator.SetFloat("Speed", animSpeed);
        SendAnimSpeedToServer(animSpeed);
    }

    [ServerRpc(RequireOwnership = true)]
    void SendAnimSpeedToServer(float animSpeed)
    {
        UpdateAnimSpeedOnClients(animSpeed);
    }

    [ObserversRpc(ExcludeOwner = true)]
    void UpdateAnimSpeedOnClients(float animSpeed)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);
        }
    }

    void UpdateHeldItemRotation()
    {
        if (heldItemPlaceholder != null && cameraTransform != null)
        {
            Vector3 euler = heldItemPlaceholder.localEulerAngles;
            euler.y = cameraTransform.eulerAngles.y;
            heldItemPlaceholder.localEulerAngles = euler;
        }
    }

    void HandleCraftingToggle()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            isCraftingOpen = !isCraftingOpen;
            craftingUI.SetActive(isCraftingOpen);

            if (isCraftingOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                FindObjectOfType<MultiplayerCraftingUI>()?.RefreshUI();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
