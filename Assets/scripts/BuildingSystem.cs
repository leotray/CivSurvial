using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingSystem : MonoBehaviour
{
    [System.Serializable]
    public class BuildingItem
    {
        public GameObject prefab;
        public Item requiredItem;
        public int requiredAmount = 1;
    }

    [Header("Building Prefabs")]
    public List<BuildingItem> buildingItems = new List<BuildingItem>();

    [Header("Settings")]
    public LayerMask groundLayer;
    public float gridSize = 1f;
    public float maxBuildDistance = 5f;

    [Header("References")]
    public Camera playerCamera;
    public InventoryManager inventoryManager;

    private GameObject currentPreview;
    private int currentPrefabIndex = 0;
    private bool isBuilding = false;
    private Quaternion currentRotation = Quaternion.identity;
    private Vector3 lastValidPosition;
    private bool lastUsedSnapPoint = false;

    // ALL ORIGINAL METHODS PRESERVED EXACTLY AS THEY WERE
    void Update()
    {
        HandleInput();

        if (isBuilding && currentPreview != null)
            UpdatePreviewPosition();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
            ToggleBuildMode();

        if (!isBuilding) return;

        if (Input.GetMouseButtonDown(0))
            TryPlaceBuilding(); // Only this method changed

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
            CyclePrefab(scroll);

        if (Input.GetKeyDown(KeyCode.R))
            RotatePreview();
    }

    void ToggleBuildMode()
    {
        isBuilding = !isBuilding;

        if (isBuilding)
            CreatePreview();
        else if (currentPreview != null)
            Destroy(currentPreview);
    }

    void CreatePreview()
    {
        if (currentPreview != null)
            Destroy(currentPreview);

        currentPreview = Instantiate(buildingItems[currentPrefabIndex].prefab);
        DisableColliders(currentPreview);
        SetTransparentMaterial(currentPreview);
    }

    void UpdatePreviewPosition()
    {
        lastUsedSnapPoint = false;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
            out RaycastHit snapHit, maxBuildDistance))
        {
            if (snapHit.collider.CompareTag("SnapPoint"))
            {
                Debug.Log("hit snappoint");
                lastValidPosition = snapHit.collider.transform.position;
                currentPreview.transform.position = lastValidPosition;
                currentPreview.transform.rotation = currentRotation;
                lastUsedSnapPoint = true;
                return;
            }
        }

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
            out RaycastHit groundHit, maxBuildDistance, groundLayer))
        {
            Vector3 point = groundHit.point;
            Vector3 snapped = new Vector3(
                Mathf.Round(point.x / gridSize) * gridSize,
                Mathf.Round(point.y / gridSize) * gridSize,
                Mathf.Round(point.z / gridSize) * gridSize
            );

            float yOffset = buildingItems[currentPrefabIndex].prefab.transform.localScale.y / 2f;
            lastValidPosition = snapped + new Vector3(0, yOffset, 0);
            currentPreview.transform.position = lastValidPosition;
            currentPreview.transform.rotation = currentRotation;
        }
    }

    void CyclePrefab(float direction)
    {
        currentPrefabIndex += direction > 0 ? 1 : -1;

        if (currentPrefabIndex >= buildingItems.Count)
            currentPrefabIndex = 0;
        else if (currentPrefabIndex < 0)
            currentPrefabIndex = buildingItems.Count - 1;

        CreatePreview();
    }

    void RotatePreview()
    {
        currentRotation *= Quaternion.Euler(0f, 90f, 0f);
        currentPreview.transform.rotation = currentRotation;
    }

    void DisableColliders(GameObject obj)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    void SetTransparentMaterial(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in renderer.materials)
            {
                Color c = mat.color;
                c.a = 0.5f;
                mat.color = c;

                mat.SetFloat("_Mode", 2);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
        }
    }

    bool IsOverlappingSameType(Vector3 position, Quaternion rotation, GameObject current)
    {
        Vector3 halfSize = Vector3.one * (gridSize / 2f);
        Collider[] overlaps = Physics.OverlapBox(position, halfSize, rotation);

        Buildable currentBuildable = current.GetComponent<Buildable>();

        foreach (var col in overlaps)
        {
            Buildable existing = col.GetComponentInParent<Buildable>();
            if (existing != null && currentBuildable != null)
            {
                if (existing.buildableType == currentBuildable.buildableType)
                    return true;
            }
        }

        return false;
    }

    void OnDrawGizmos()
    {
        if (isBuilding)
        {
            Gizmos.color = lastUsedSnapPoint ? Color.green : Color.red;
            Gizmos.DrawSphere(lastValidPosition, 0.1f);
        }
    }

    // ONLY CHANGED METHOD - Now checks inventory
    void TryPlaceBuilding()
    {
        if (currentPreview == null) return;

        BuildingItem currentBuilding = buildingItems[currentPrefabIndex];

        // 1. Check inventory
        if (!inventoryManager.HasItem(currentBuilding.requiredItem, currentBuilding.requiredAmount))
        {
            Debug.Log($"Need {currentBuilding.requiredAmount} {currentBuilding.requiredItem.itemName} to build!");
            return;
        }

        // 2. Original placement check
        if (IsOverlappingSameType(lastValidPosition, currentRotation, currentBuilding.prefab))
        {
            Debug.Log("Cannot place: same type already exists here.");
            return;
        }

        // 3. Deduct items and place
        if (inventoryManager.RemoveItem(currentBuilding.requiredItem, currentBuilding.requiredAmount))
        {
            Instantiate(currentBuilding.prefab, lastValidPosition, currentRotation);
        }
    }
    public void AddNewBuildingOption(BuildingItem newBuilding)
    {
        if (!buildingItems.Contains(newBuilding))
        {
            buildingItems.Add(newBuilding);
            // Refresh UI if needed
        }
    }
}