using UnityEngine;

public class HorseMounting : MonoBehaviour
{
    public float mountRange = 3f;
    public KeyCode dismountKey = KeyCode.LeftShift;
    public KeyCode mountKey = KeyCode.E;

    private bool isMounted = false;
    private Transform mountPoint;
    private Transform currentHorse;
    private HorseController horseController;

    private PlayerMovement playerMovement;
    private Rigidbody rb;
    private InventoryManager inventoryManager;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        inventoryManager = GetComponent<InventoryManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(mountKey))
        {
            if (!isMounted) TryMount();
            else Dismount();
        }

        if (isMounted && Input.GetKeyDown(dismountKey))
        {
            Dismount();
        }

        if (isMounted && mountPoint != null)
        {
            transform.position = mountPoint.position;
            transform.rotation = mountPoint.rotation;
        }
    }

    void TryMount()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f))
        {
            MountableHorse horse = hit.collider.GetComponentInParent<MountableHorse>();
            if (horse == null)
            {
                Debug.Log("⛔ Not pointing at a horse.");
                return;
            }

            if (!horse.isTamed)
            {
                Debug.Log("🛑 Cannot mount: horse not tamed.");
                return;
            }

            Debug.Log("✅ Mounting horse...");
            Mount(horse);
        }
    }

    void Mount(MountableHorse horse)
    {
        currentHorse = horse.transform;
        mountPoint = horse.mountPoint;

        horseController = currentHorse.GetComponent<HorseController>();
        if (horseController != null)
            horseController.SetControlled(true, transform);

        if (playerMovement) playerMovement.enabled = false;
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.SetParent(mountPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        isMounted = true;
    }

    void Dismount()
    {
        if (!isMounted) return;

        transform.SetParent(null);
        if (playerMovement) playerMovement.enabled = true;
        if (rb) rb.isKinematic = false;

        transform.position = mountPoint.position + currentHorse.right * 2f;

        if (horseController != null)
        {
            horseController.SetControlled(false);
            horseController = null;
        }

        isMounted = false;
        currentHorse = null;
        mountPoint = null;
    }
}
