using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;

public class ConstructionInteraction : NetworkBehaviour
{
    private Camera cam;
    private MPInventoryManager inventory;

    // cooldown between contributions (seconds)
    private float contributeCooldown = 0.25f;
    private float lastContributeTime = -999f;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        inventory = GetComponent<MPInventoryManager>();

        if (Owner.IsLocalClient)
            StartCoroutine(WaitForCameraCoroutine());
    }

    private IEnumerator WaitForCameraCoroutine()
    {
        int attempts = 0;
        while (cam == null && attempts < 60)
        {
            cam = GetComponentInChildren<Camera>(true) ?? Camera.main;
            if (cam != null) yield break;
            attempts++;
            yield return null;
        }

        if (cam == null)
            Debug.LogWarning("[ConstructionInteraction] Camera not found after attempts.");
    }

    private CityBlockConstructionSite lastLookedSite;

    private void Update()
    {
        if (!IsOwner) return;

        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true) ?? Camera.main;
            if (cam == null) return;
        }

        // Look check every frame
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, 4f))
        {
            var site = hit.collider.GetComponent<CityBlockConstructionSite>();
            if (site != null)
            {
                lastLookedSite = site;
                float percent = site.GetCompletionPercent();
                ConstructionProgressUI.Instance?.ShowProgress(percent);
            }
            else
            {
                lastLookedSite = null;
                ConstructionProgressUI.Instance?.HideProgress();
            }
        }
        else
        {
            lastLookedSite = null;
            ConstructionProgressUI.Instance?.HideProgress();
        }

        // Contribute check
        if (Input.GetKeyDown(KeyCode.E))
            TryContribute();
    }

    private void TryContribute()
    {
        if (Time.time - lastContributeTime < contributeCooldown)
            return; // local rate limiting

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, 4f))
        {
            CityBlockConstructionSite site = hit.collider.GetComponent<CityBlockConstructionSite>();
            if (site != null)
            {
                var selectedItem = inventory?.GetSelectedItemClient();
                if (selectedItem != null)
                {
                    // local optimistic cooldown
                    lastContributeTime = Time.time;
                    // Send the GUID to the server instead of full item
                    CmdContributeResource(site.NetworkObject, selectedItem.Value.itemGuid);
                }
                else
                {
                    Debug.Log("No selected item to contribute.");
                }
            }
        }
    }

    // ServerRpc runs on server. The server will validate & remove item from server-side inventory.
    [ServerRpc]
    private void CmdContributeResource(NetworkObject siteObj, string itemGuid)
    {
        if (siteObj == null || string.IsNullOrEmpty(itemGuid)) return;

        var site = siteObj.GetComponent<CityBlockConstructionSite>();
        if (site == null) return;

        // Server-side inventory on the same player object (this NetworkBehaviour is on the player object)
        var serverInventory = GetComponent<MPInventoryManager>();
        if (serverInventory == null)
        {
            Debug.LogWarning("Server: MPInventoryManager not found on player object.");
            return;
        }

        // Validate distance: require player to be near the site
        float dist = Vector3.Distance(site.transform.position, transform.position);
        if (dist > site.contributionRadius)
        {
            Debug.Log($"Server: Player {Owner.ClientId} too far to contribute ({dist:F2} > {site.contributionRadius})");
            return;
        }

        // How many still needed of this item?
        int stillNeeded = site.GetRemainingForItem(itemGuid);
        if (stillNeeded <= 0)
        {
            Debug.Log($"Server: Site does not need item {itemGuid} anymore.");
            return;
        }

        // Check server-side inventory total for item
        if (!serverInventory.HasItem(itemGuid, 1))
        {
            Debug.LogWarning($"Server: Player {Owner.ClientId} attempted to contribute item they don't have: {itemGuid}");
            return;
        }

        // Remove one (or up to stillNeeded if you wanted batch)
        int removed = serverInventory.RemoveItem(itemGuid, 1);
        if (removed <= 0)
        {
            Debug.LogWarning($"Server: Failed to remove item {itemGuid} from player {Owner.ClientId}");
            return;
        }

        // All validated — apply contribution on server
        site.ContributeResourceFromPlayer(itemGuid, removed, Owner);
    }
}
