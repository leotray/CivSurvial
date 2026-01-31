using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;

public class CityBlockConstructionSite : NetworkBehaviour
{
    [System.Serializable]
    public struct ResourceRequirement
    {
        public string itemId;
        public int requiredAmount;
        public int currentAmount;
    }

    [Header("Resource Requirements")]
    [SerializeField] private List<ResourceRequirement> startingRequirements = new List<ResourceRequirement>();

    public float contributionRadius = 4f;
    public float spawnHeightRaycastStart = 10f;

    public readonly SyncList<ResourceRequirement> requirements = new SyncList<ResourceRequirement>();
    public readonly SyncVar<bool> isCompleted = new SyncVar<bool>();

    private Dictionary<ulong, int> contributors = new Dictionary<ulong, int>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[CityBlock] SERVER START — Initializing requirements...");
        requirements.Clear();
        foreach (var req in startingRequirements)
        {
            requirements.Add(new ResourceRequirement
            {
                itemId = req.itemId,
                requiredAmount = req.requiredAmount,
                currentAmount = 0
            });
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("[CityBlock] CLIENT START — Listening for changes...");
        requirements.OnChange += OnRequirementsChanged;
        isCompleted.OnChange += OnCompletionChanged;
    }

    private void OnRequirementsChanged(SyncListOperation op, int index, ResourceRequirement oldItem, ResourceRequirement newItem, bool asServer)
    {
        Debug.Log($"[CityBlock] Requirement changed — now {GetCompletionPercent() * 100f:F1}% complete");
        if (IsOwner)
            ConstructionProgressUI.Instance?.ShowProgress(GetCompletionPercent());
    }

    private void OnCompletionChanged(bool oldValue, bool newValue, bool asServer)
    {
        Debug.Log($"[CityBlock] OnCompletionChanged — Old: {oldValue}, New: {newValue}, AsServer: {asServer}");
    }

    public int GetRemainingForItem(string itemId)
    {
        int idx = requirements.FindIndex(r => r.itemId == itemId);
        if (idx == -1)
            return 0;

        var req = requirements[idx];
        return Mathf.Max(0, req.requiredAmount - req.currentAmount);
    }

    [Server]
    public void ContributeResourceFromPlayer(string itemGuid, int amount, NetworkConnection contributor)
    {
        Debug.Log($"[CityBlock] Contribution — Item: {itemGuid}, Amount: {amount}, ContributorId: {contributor?.ClientId}");

        if (isCompleted.Value)
        {
            Debug.LogWarning("[CityBlock] Ignored contribution — construction already completed.");
            return;
        }

        int idx = requirements.FindIndex(r => r.itemId == itemGuid);
        if (idx == -1)
        {
            Debug.LogWarning($"[CityBlock] No matching requirement for item {itemGuid}");
            return;
        }

        var req = requirements[idx];
        int remaining = req.requiredAmount - req.currentAmount;
        int toAccept = Mathf.Min(remaining, amount);
        if (toAccept <= 0)
        {
            Debug.LogWarning($"[CityBlock] Requirement for {itemGuid} already met.");
            return;
        }

        req.currentAmount += toAccept;
        requirements[idx] = req; // ✅ important — updates SyncList

        ulong clientId = (ulong)contributor.ClientId;
        if (!contributors.ContainsKey(clientId))
            contributors[clientId] = 0;
        contributors[clientId] += toAccept;

        Debug.Log($"[CityBlock] Progress: {GetCompletionPercent() * 100f:F1}%");

        if (GetCompletionPercent() >= 1f)
        {
            Debug.Log("[CityBlock] All requirements met! Completing construction...");
            isCompleted.Value = true;
            ReplaceWithCityCenter();
        }
    }

    [Server]
    private void ReplaceWithCityCenter()
    {
        Debug.Log("[CityBlock] Replacing construction site with CityCenter prefab...");

        GameObject prefab = Resources.Load<GameObject>("CityCenter");
        if (prefab == null)
        {
            Debug.LogError("[CityBlock] ERROR: CityCenter prefab not found in Resources!");
            return;
        }

        // pick spawn position
        Vector3 spawnPos = transform.position;
        if (Physics.Raycast(spawnPos + Vector3.up * spawnHeightRaycastStart, Vector3.down, out RaycastHit hit))
            spawnPos = hit.point;

        // 1️⃣ Create the CityData in CityManager
        int newCityId = CityManager.Instance.CreateCity("New City", -1, spawnPos);
        // -1 faction = independent (until leader decides)

        // 2️⃣ Spawn the prefab and link CityId
        GameObject newCityObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        Spawn(newCityObj);

        City cityComp = newCityObj.GetComponent<City>();
        if (cityComp != null)
        {
            // link this MonoBehaviour to its CityData
            cityComp.Initialize(newCityId);

            // register contributors as citizens
            foreach (var kvp in contributors)
                CityManager.Instance.AddCitizen(newCityId, kvp.Key);

            // 3️⃣ mark hall built in manager
            CityManager.Instance.MarkCityHallBuilt(newCityId);

            // 4️⃣ still call election UI from City component
            cityComp.InitializeContributors(contributors);
            cityComp.MarkCityHallBuilt();
            cityComp.RpcOpenElectionUI(new List<ulong>(contributors.Keys));

            Debug.Log($"[CityBlock] Finished creating city with ID {newCityId}, contributors {contributors.Count}");
        }
        else
        {
            Debug.LogError("[CityBlock] ERROR: City component missing on CityCenter prefab!");
        }
        // 5️⃣ Spawn Banner in front of CityHall
        GameObject bannerPrefab = Resources.Load<GameObject>("Banner");
        if (bannerPrefab == null)
        {
            Debug.LogError("[CityBlock] ERROR: Banner prefab not found in Resources!");
        }
        else
        {
            Vector3 bannerPos = spawnPos + (Vector3.forward * 20f); // adjust offset as needed
            GameObject bannerObj = Instantiate(bannerPrefab, bannerPos, Quaternion.identity);
            Spawn(bannerObj);

            Banner bannerComp = bannerObj.GetComponent<Banner>();
            if (bannerComp != null)
                bannerComp.Initialize(newCityId);
        }

        // finally despawn construction site
        Despawn(gameObject);
    }


    public float GetCompletionPercent()
    {
        int totalRequired = 0, totalCurrent = 0;
        foreach (var req in requirements)
        {
            totalRequired += req.requiredAmount;
            totalCurrent += Mathf.Min(req.currentAmount, req.requiredAmount);
        }
        return totalRequired > 0 ? (float)totalCurrent / totalRequired : 1f;
    }
}
