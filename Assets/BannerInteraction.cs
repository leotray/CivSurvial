using UnityEngine;
using FishNet.Object;
using FishNet;

public class BannerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private LayerMask bannerLayer;

    private Camera mainCamera;
    private Vector3 lastRayOrigin;
    private Vector3 lastRayDir;
    private bool lastRayHit;
    private Transform lastHitTransform;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                Debug.LogError("[BannerInteraction] Main Camera not found!");
            else
                Debug.Log("[BannerInteraction] Main Camera successfully assigned.");
        }
    }

    void Update()
    {
        if (!IsOwner) return;
        if (mainCamera == null) return;

        if (Input.GetMouseButtonDown(1)) // Right-click
        {
            Debug.Log("[BannerInteraction] Right-click detected. Trying to interact with banner...");
            TryInteractWithBanner();
        }
    }

    private void TryInteractWithBanner()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        lastRayOrigin = ray.origin;
        lastRayDir = ray.direction;

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, bannerLayer))
        {
            lastRayHit = true;
            lastHitTransform = hit.transform;

            Debug.Log($"[BannerInteraction] Raycast HIT {hit.collider.name} at distance {hit.distance}");

            Banner banner = hit.collider.GetComponent<Banner>();
            if (banner != null)
            {
                Debug.Log($"[BannerInteraction] Found Banner component on {banner.name}, sending ServerRpc...");
                banner.RequestOpenCityMenuServerRpc((ulong)Owner.ClientId);
            }
            else
            {
                Debug.LogWarning("[BannerInteraction] Collider hit has NO Banner component!");
            }
        }
        else
        {
            lastRayHit = false;
            lastHitTransform = null;
            Debug.LogWarning("[BannerInteraction] Raycast did NOT hit any banner.");
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = lastRayHit ? Color.green : Color.red;
        Gizmos.DrawLine(lastRayOrigin, lastRayOrigin + lastRayDir * interactDistance);

        if (lastHitTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastHitTransform.position, 0.2f);
        }
    }
}
