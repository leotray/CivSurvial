using UnityEngine;

public class TransportInspector : MonoBehaviour
{
    [ContextMenu("ListCandidateTransports")]
    void ListCandidateTransports()
    {
        var all = FindObjectsOfType<MonoBehaviour>(true);
        Debug.Log($"[TransportInspector] Scanning {all.Length} MonoBehaviours...");
        foreach (var comp in all)
        {
            var tname = comp.GetType().Name.ToLower();
            if (tname.Contains("transport") || tname.Contains("fishy") || tname.Contains("unitytransport") || tname.Contains("utp"))
                Debug.Log($"[TransportInspector] Found: {comp.GetType().FullName} on GameObject '{comp.gameObject.name}'");
        }
    }
    [ContextMenu("CheckNetworkManagerTransportManager")]
    void CheckNM()
    {
        var nm = FindObjectOfType<FishNet.Managing.NetworkManager>();
        if (nm == null) Debug.LogWarning("[TransportInspector] No NetworkManager found.");
        else Debug.Log($"[TransportInspector] NetworkManager found. TransportManager component: {nm.TransportManager != null}");
    }

}
