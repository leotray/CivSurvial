using UnityEngine;
using FishNet.Object;
using System.Collections;

public class NetworkedBuildingSystem : NetworkBehaviour
{
    [Header("Prefabs & preview (fallbacks)")]
    public GameObject[] buildPrefabs;
    public Material previewMaterial;

    [Header("Snapping & grid")]
    public float gridSize = 1f;
    public float snapRange = 0.6f;
    public LayerMask buildableLayer;

    [Header("Raycast settings")]
    public float raycastDistance = 8f;
    public LayerMask raycastLayers = ~0;

    [Header("Input keys")]
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    public KeyCode placeKey = KeyCode.Mouse0;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Role Multipliers")]
    [Tooltip("How many items to consume per build action. Lower is better.")]
    public int buildingCostMultiplier = 2; // default, builder overrides to 1

    private MPInventoryManager inventoryManager;
    private GameObject previewObj;
    private bool isBuildMode = false;
    private Camera cam;
    private Quaternion previewRotation = Quaternion.identity;

    private RaycastHit currentHit;
    private bool hasHit = false;
    private bool canBuildHere = false;
    private string currentPreviewGuid = "";

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (Owner.IsLocalClient)
            StartCoroutine(WaitForCameraCoroutine());

        inventoryManager = GetComponent<MPInventoryManager>();
        if (inventoryManager == null)
            Debug.LogWarning("NetworkedBuildingSystem: MPInventoryManager not found on same GameObject.");
    }

    private IEnumerator WaitForCameraCoroutine()
    {
        int attempts = 0;
        while (cam == null && attempts < 60)
        {
            cam = GetComponentInChildren<Camera>(true);
            if (cam != null) yield break;

            cam = Camera.main;
            if (cam != null) yield break;

            attempts++;
            yield return null;
        }
        if (cam == null) Debug.LogWarning("[NetworkedBuildingSystem] Camera not found after attempts.");
    }

    void Update()
    {
        if (!IsOwner) return;

        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true) ?? Camera.main;
            if (cam == null) return;
        }

        AutoToggleBySelectedItem();

        if (!isBuildMode)
            return;

        HandleRotationInput();
        UpdatePreviewPosition();

        if (Input.GetKeyDown(placeKey))
            TryPlace();

        if (Input.GetKeyDown(cancelKey))
            ExitBuildMode();
    }

    void AutoToggleBySelectedItem()
    {
        if (inventoryManager == null) return;
        if (!inventoryManager.IsOwner) return;

        if (inventoryManager.selectedSlot >= 0 && inventoryManager.selectedSlot < inventoryManager.inventory.Count)
        {
            var invItm = inventoryManager.inventory[inventoryManager.selectedSlot];
            ItemData data = GameData.LookupItem(invItm.itemGuid);
            if (data != null && data.itemCategory == ItemData.ItemCategory.BuildingBlock && data.buildPrefab != null)
            {
                if (!isBuildMode || currentPreviewGuid != data.guid)
                {
                    isBuildMode = true;
                    currentPreviewGuid = data.guid;
                    CreatePreviewFromItem(data);
                }
                return;
            }
        }

        if (isBuildMode)
            ExitBuildMode();
    }

    void HandleRotationInput()
    {
        if (previewObj == null) return;

        if (Input.GetKeyDown(rotateLeftKey))
            previewRotation *= Quaternion.Euler(0f, -90f, 0f);
        if (Input.GetKeyDown(rotateRightKey))
            previewRotation *= Quaternion.Euler(0f, 90f, 0f);

        previewObj.transform.rotation = previewRotation;
    }

    void CreatePreviewFromItem(ItemData item)
    {
        DestroyPreview();

        GameObject prefab = item.buildPrefab != null ? item.buildPrefab : null;

        if (prefab == null && buildPrefabs != null && buildPrefabs.Length > 0)
        {
            prefab = System.Array.Find(buildPrefabs, b => b != null && b.name == item.itemName);
            if (prefab == null)
                prefab = buildPrefabs[0];
        }

        if (prefab == null)
        {
            Debug.LogWarning("No build prefab found for item " + item.itemName);
            return;
        }

        previewObj = Instantiate(prefab);
        previewObj.name = $"Preview_{prefab.name}";

        var buildableComp = previewObj.GetComponent<BuildableNetworked>();
        if (buildableComp != null) DestroyImmediate(buildableComp);

        var netObj = previewObj.GetComponent<NetworkObject>();
        if (netObj != null) DestroyImmediate(netObj);

        foreach (var netBehaviour in previewObj.GetComponentsInChildren<NetworkBehaviour>())
            DestroyImmediate(netBehaviour);

        foreach (var col in previewObj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (previewMaterial != null)
        {
            var renderers = previewObj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = previewMaterial;
                r.materials = mats;
            }
        }

        previewRotation = previewObj.transform.rotation;
    }

    void DestroyPreview()
    {
        if (previewObj != null)
            Destroy(previewObj);
        previewObj = null;
        currentPreviewGuid = "";
    }

    void ExitBuildMode()
    {
        isBuildMode = false;
        DestroyPreview();
    }

    void UpdatePreviewPosition()
    {
        if (previewObj == null || cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayers))
        {
            currentHit = hit;
            hasHit = true;

            if (TryGetSnapPosition(hit.point, out Vector3 snappedPos, out Quaternion snappedRot))
            {
                previewObj.transform.rotation = snappedRot * previewRotation;
                previewObj.transform.position = snappedPos;
                canBuildHere = true;
            }
            else
            {
                Vector3 gridPos = GetGridPosition(hit.point);
                previewObj.transform.position = gridPos;
                previewObj.transform.rotation = previewRotation;
                canBuildHere = (hit.collider != null && ((1 << hit.collider.gameObject.layer) & buildableLayer) != 0);
            }
        }
        else
        {
            hasHit = false;
            canBuildHere = false;
        }
    }

    Vector3 GetGridPosition(Vector3 worldPos)
    {
        float x = Mathf.Round(worldPos.x / gridSize) * gridSize;
        float y = Mathf.Round(worldPos.y / gridSize) * gridSize;
        float z = Mathf.Round(worldPos.z / gridSize) * gridSize;
        return new Vector3(x, y, z);
    }

    bool AreOppositeDirections(SnapDirection a, SnapDirection b)
    {
        return (a == SnapDirection.North && b == SnapDirection.South) ||
               (a == SnapDirection.South && b == SnapDirection.North) ||
               (a == SnapDirection.East && b == SnapDirection.West) ||
               (a == SnapDirection.West && b == SnapDirection.East) ||
               (a == SnapDirection.Up && b == SnapDirection.Down) ||
               (a == SnapDirection.Down && b == SnapDirection.Up);
    }

    bool TryGetSnapPosition(Vector3 fromPoint, out Vector3 outPosition, out Quaternion outRotation)
    {
        outPosition = Vector3.zero;
        outRotation = Quaternion.identity;

        if (previewObj == null) return false;

        Collider[] hits = Physics.OverlapSphere(fromPoint, snapRange, buildableLayer);
        if (hits.Length == 0) return false;

        SnapPoint[] previewSnapPoints = previewObj.GetComponentsInChildren<SnapPoint>(true);
        if (previewSnapPoints.Length == 0) return false;

        float bestScore = float.MaxValue;
        bool found = false;
        Vector3 bestPos = Vector3.zero;
        Quaternion bestRot = Quaternion.identity;

        foreach (var hitCol in hits)
        {
            SnapPoint[] targetSnaps = hitCol.GetComponentsInChildren<SnapPoint>(true);
            if (targetSnaps.Length == 0) continue;

            foreach (var target in targetSnaps)
            {
                foreach (var previewSnap in previewSnapPoints)
                {
                    if (!AreOppositeDirections(previewSnap.direction, target.direction))
                        continue;

                    Quaternion rotationOffset = target.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(previewSnap.transform.rotation);

                    Vector3 previewLocalPos = previewSnap.transform.localPosition;
                    Vector3 desiredRootPos = target.transform.position - (rotationOffset * previewLocalPos);

                    float score = Vector3.Distance(desiredRootPos, fromPoint);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPos = desiredRootPos;
                        bestRot = rotationOffset;
                        found = true;
                    }
                }
            }
        }

        if (found)
        {
            outPosition = bestPos;
            outRotation = bestRot;
            return true;
        }

        return false;
    }

    void TryPlace()
    {
        if (previewObj == null || !canBuildHere) return;
        if (inventoryManager == null) return;

        int slotIndex = inventoryManager.selectedSlot;
        if (slotIndex < 0 || slotIndex >= inventoryManager.inventory.Count) return;
        var invItem = inventoryManager.inventory[slotIndex];
        ItemData item = GameData.LookupItem(invItem.itemGuid);
        if (item == null || item.itemCategory != ItemData.ItemCategory.BuildingBlock) return;

        int cost = Mathf.Max(1, buildingCostMultiplier);
        // <-- FIX: use 'count' not 'amount'
        if (invItem.count < cost)
        {
            Debug.Log("Not enough resources to build!");
            return;
        }

        Vector3 pos = previewObj.transform.position;
        Quaternion rot = previewObj.transform.rotation;

        PlaceBuildingServerRpc(item.guid, slotIndex, pos, rot, cost);
    }

    [ServerRpc(RequireOwnership = false)]
    void PlaceBuildingServerRpc(string itemGuid, int slotIndex, Vector3 position, Quaternion rotation, int cost)
    {
        if (inventoryManager == null)
        {
            inventoryManager = GetComponent<MPInventoryManager>();
            if (inventoryManager == null) return;
        }

        if (slotIndex < 0 || slotIndex >= inventoryManager.inventory.Count) return;
        var invItem = inventoryManager.inventory[slotIndex];
        if (invItem.itemGuid != itemGuid) return;

        // <-- FIX: use 'count' not 'amount'
        if (invItem.count < cost)
        {
            Debug.Log("Server: Not enough resources, build cancelled.");
            return;
        }

        ItemData item = GameData.LookupItem(itemGuid);
        if (item == null || item.itemCategory != ItemData.ItemCategory.BuildingBlock) return;

        GameObject prefabToSpawn = item.buildPrefab;
        if (prefabToSpawn == null)
        {
            if (buildPrefabs != null && buildPrefabs.Length > 0) prefabToSpawn = buildPrefabs[0];
            else return;
        }

        GameObject go = Instantiate(prefabToSpawn, position, rotation);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogWarning("Placed prefab is missing NetworkObject component.");
            Destroy(go);
            return;
        }

        Spawn(netObj);

        // Deduct resources
        inventoryManager.RemoveFromSlotServer(slotIndex, cost);
    }

    public void SelectPrefabIndex(int idx) { }

    void OnDrawGizmos()
    {
        if (!hasHit || cam == null) return;
        Gizmos.color = canBuildHere ? Color.green : Color.red;
        Gizmos.DrawLine(cam.transform.position, currentHit.point);
        Gizmos.DrawSphere(currentHit.point, 0.1f);
    }
}
