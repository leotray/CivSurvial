// RelayManager.cs
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Robust RelayManager that configures transports (FishyUnityTransport / Unity Transport wrappers)
/// via reflection. Handles Allocation vs JoinAllocation and chooses the correct RelayServerData ctor.
/// Improved candidate selection to avoid accidental picks like user helper scripts named "*TransportInspector*".
/// </summary>
public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance;

    public bool LastConfigurationSuccess { get; private set; } = false;
    public Allocation LastAllocation { get; private set; } = null;
    public JoinAllocation LastJoinAllocation { get; private set; } = null;

    [Tooltip("Timeout in milliseconds for Relay create/join operations.")]
    public int RelayOperationTimeoutMs = 10000;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public async Task<string> CreateRelay(int maxPlayers = 10)
    {
        LastConfigurationSuccess = false;
        LastAllocation = null;
        try
        {
            Debug.Log("[RelayManager] Creating relay allocation...");
            var createTask = RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            if (!await WaitTaskWithTimeout(createTask, RelayOperationTimeoutMs))
            {
                Debug.LogError("[RelayManager] CreateAllocationAsync timed out.");
                return null;
            }

            Allocation alloc = createTask.Result;
            LastAllocation = alloc;

            var joinCodeTask = RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            if (!await WaitTaskWithTimeout(joinCodeTask, RelayOperationTimeoutMs))
            {
                Debug.LogError("[RelayManager] GetJoinCodeAsync timed out.");
                return null;
            }

            string joinCode = joinCodeTask.Result;
            Debug.Log($"[RelayManager] Relay created. JoinCode = '{joinCode}', Region = {alloc.Region}");

            bool ok = TryConfigureTransportForAllocation(alloc);
            LastConfigurationSuccess = ok;
            Debug.Log($"[RelayManager] ConfigureTransportForAllocation returned: {ok}");

            return ok ? joinCode : null;
        }
        catch (RelayServiceException rse)
        {
            Debug.LogError("[RelayManager] CreateRelay RelayServiceException: " + rse);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError("[RelayManager] CreateRelay unexpected error: " + ex);
            return null;
        }
    }

    public async Task<JoinAllocation> JoinRelay(string joinCode)
    {
        LastConfigurationSuccess = false;
        LastJoinAllocation = null;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogError("[RelayManager] JoinRelay called with empty joinCode.");
            return null;
        }

        joinCode = joinCode.Trim();
        Debug.Log($"[RelayManager] Joining relay with sanitized code '{joinCode}'");

        try
        {
            var joinTask = RelayService.Instance.JoinAllocationAsync(joinCode);
            if (!await WaitTaskWithTimeout(joinTask, RelayOperationTimeoutMs))
            {
                Debug.LogError("[RelayManager] JoinAllocationAsync timed out.");
                return null;
            }

            JoinAllocation joinAlloc = joinTask.Result;
            LastJoinAllocation = joinAlloc;

            Debug.Log("[RelayManager] JoinAllocation received. Region: " + (joinAlloc?.Region ?? "null"));

            bool ok = TryConfigureTransportForJoinAllocation(joinAlloc);
            LastConfigurationSuccess = ok;
            Debug.Log($"[RelayManager] ConfigureTransportForJoinAllocation returned: {ok}");

            return ok ? joinAlloc : null;
        }
        catch (RelayServiceException rse)
        {
            Debug.LogError("[RelayManager] JoinRelay RelayServiceException: " + rse);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError("[RelayManager] JoinRelay unexpected error: " + ex);
            return null;
        }
    }

    private async Task<bool> WaitTaskWithTimeout(Task t, int timeoutMs)
    {
        var delay = Task.Delay(timeoutMs);
        var completed = await Task.WhenAny(t, delay);
        return completed == t;
    }

    #region Reflection helpers

    /// <summary>
    /// Search for the best candidate transport component in the scene.
    /// Heuristics:
    ///  - prefer types with names matching known transports (UnityTransport, FishyUnityTransport, UTP, FishNet.Transporting)
    ///  - prefer components that expose methods accepting Allocation/JoinAllocation or RelayServerData or a SetRelay* method
    ///  - avoid obvious non-transport helpers (TransportInspector etc.)
    /// </summary>
    private Component FindCandidateTransportComponent()
    {
        var all = FindObjectsOfType<MonoBehaviour>(true);
        Component best = null;
        int bestScore = -1;

        foreach (var comp in all)
        {
            Type t = comp.GetType();
            string name = t.Name.ToLowerInvariant();

            // cheap rejects: don't pick scripts that are clearly editor/debug helpers
            if (name.Contains("inspector") || name.Contains("debugger") || name.Contains("tester"))
                continue;

            int score = 0;

            // name hints
            if (name.Contains("fishy") || name.Contains("fishynet")) score += 50;
            if (name.Contains("unitytransport") || name.Contains("unitytransporting") || name.Contains("utp")) score += 40;
            if (name.Contains("unitytransport") || name.Contains("unity.transport")) score += 40;
            if (name.Contains("transport")) score += 10;

            // methods check: does it have a SetRelayServerData or method accepting Allocation/JoinAllocation?
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var m in methods)
            {
                string mname = m.Name.ToLowerInvariant();
                if (mname.Contains("setrelay") || mname.Contains("relay"))
                {
                    score += 30;
                    break;
                }
                var ps = m.GetParameters();
                if (ps.Length == 1 && (ps[0].ParameterType.Name.ToLowerInvariant().Contains("allocation") || ps[0].ParameterType.Name.ToLowerInvariant().Contains("relay")))
                {
                    score += 25;
                    break;
                }
            }

            // fields/properties looking for allocation/relay fields
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                var fname = f.Name.ToLowerInvariant();
                if (fname.Contains("allocation") || fname.Contains("relay")) { score += 10; break; }
            }

            // prefer components on the NetworkManager GameObject (common case)
            if (comp.gameObject.name.ToLowerInvariant().Contains("networkmanager")) score += 15;

            if (score > bestScore)
            {
                bestScore = score;
                best = comp;
            }
        }

        if (best != null)
            Debug.Log($"[RelayManager] Candidate transport component selected: {best.GetType().FullName} on '{best.gameObject.name}' (score {bestScore})");
        else
            Debug.LogWarning("[RelayManager] No transport-like component found in scene. Ensure FishyUnityTransport or Unity Transport is present and on NetworkManager.");

        return best;
    }

    private bool TryConfigureTransportForAllocation(Allocation alloc)
    {
        var comp = FindCandidateTransportComponent();
        if (comp == null)
        {
            Debug.LogError("[RelayManager] No transport candidate found for Allocation configuration.");
            return false;
        }
        return TryConfigureTransportWithReflection(comp, alloc);
    }

    private bool TryConfigureTransportForJoinAllocation(JoinAllocation joinAlloc)
    {
        var comp = FindCandidateTransportComponent();
        if (comp == null)
        {
            Debug.LogError("[RelayManager] No transport candidate found for JoinAllocation configuration.");
            return false;
        }
        return TryConfigureTransportWithReflection(comp, joinAlloc);
    }

    /// <summary>
    /// Try to configure the given transport component (or any nested inner transport) using reflection.
    /// </summary>
    private bool TryConfigureTransportWithReflection(Component transportComp, object relayAllocObject)
    {
        if (transportComp == null || relayAllocObject == null)
            return false;

        Type transportType = transportComp.GetType();
        Debug.Log($"[RelayManager] Attempting to configure transport ({transportType.FullName}) with {relayAllocObject.GetType().Name}");

        // 1) Try direct invocation on the component
        if (TryInvokeRelayMethodOnObject(transportComp, relayAllocObject))
            return true;

        // 2) Try nested fields/properties inside the component (wrappers may hold an inner UnityTransport)
        var members = transportType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var m in members)
        {
            object inner = null;
            try
            {
                if (m is FieldInfo fi) inner = fi.GetValue(transportComp);
                else if (m is PropertyInfo pi && pi.CanRead) inner = pi.GetValue(transportComp);
            }
            catch { inner = null; }

            if (inner == null) continue;
            if (!(inner is UnityEngine.Object)) continue;

            string innerName = inner.GetType().Name.ToLowerInvariant();
            if (!(innerName.Contains("transport") || innerName.Contains("unity") || innerName.Contains("utp") || innerName.Contains("relay")))
                continue;

            Debug.Log($"[RelayManager] Trying nested component '{inner.GetType().FullName}' (member {m.Name})");
            if (TryInvokeRelayMethodOnObject(inner, relayAllocObject))
                return true;

            if (TrySetAllocationFieldOnObject(inner, relayAllocObject))
                return true;
        }

        // 3) As a last resort: scan the whole scene for any component with a suitable method/field
        var all = FindObjectsOfType<MonoBehaviour>(true);
        foreach (var comp in all)
        {
            Type t = comp.GetType();
            string tname = t.Name.ToLowerInvariant();
            if (!(tname.Contains("unitytransport") || tname.Contains("utp") || tname.Contains("unitytransporting") || tname.Contains("fishy") || tname.Contains("relay")))
                continue;

            Debug.Log($"[RelayManager] Trying scene transport candidate '{t.FullName}' on '{comp.gameObject.name}'");
            if (TryInvokeRelayMethodOnObject(comp, relayAllocObject)) return true;
            if (TrySetAllocationFieldOnObject(comp, relayAllocObject)) return true;
        }

        Debug.LogError("[RelayManager] Failed to configure transport for Relay. Make sure a transport that supports Relay is attached (FishyUnityTransport or Unity Transport wrapper).");
        return false;
    }

    /// <summary>
    /// Try to invoke a method on target that either accepts Allocation/JoinAllocation, or RelayServerData.
    /// Properly selects RelayServerData ctor based on the exact allocation type.
    /// </summary>
    private bool TryInvokeRelayMethodOnObject(object targetObj, object relayAllocObject)
    {
        Type t = targetObj.GetType();
        MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Candidate methods: method name contains "relay" OR parameter type contains "allocation"/"relay"
        var candidateMethods = methods.Where(m =>
        {
            var ps = m.GetParameters();
            return ps.Length == 1 && (m.Name.ToLowerInvariant().Contains("relay")
                || ps[0].ParameterType.Name.ToLowerInvariant().Contains("allocation")
                || ps[0].ParameterType.Name.ToLowerInvariant().Contains("relay"));
        }).ToArray();

        foreach (var method in candidateMethods)
        {
            var paramType = method.GetParameters()[0].ParameterType;
            Debug.Log($"[RelayManager] Found candidate method '{method.Name}' on {t.FullName}, param: {paramType.FullName}");

            try
            {
                // If the method directly accepts the allocation type (Allocation or JoinAllocation), call it directly
                if (paramType.IsInstanceOfType(relayAllocObject))
                {
                    method.Invoke(targetObj, new[] { relayAllocObject });
                    Debug.Log($"[RelayManager] Invoked {method.Name} with allocation object directly on {t.FullName}");
                    return true;
                }

                // Otherwise attempt to construct RelayServerData (correct ctor for Allocation vs JoinAllocation)
                Type relayDataType = FindTypeByName("RelayServerData");
                if (relayDataType != null)
                {
                    // Look for ctor that expects (Allocation, string) or (JoinAllocation, string)
                    ConstructorInfo chosenCtor = null;
                    var ctors = relayDataType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var c in ctors)
                    {
                        var ps = c.GetParameters();
                        if (ps.Length != 2) continue;
                        var first = ps[0].ParameterType;
                        var second = ps[1].ParameterType;
                        if (second != typeof(string)) continue;

                        // exact match: if relayAllocObject is Allocation and ctor accepts Allocation
                        if (relayAllocObject is Allocation && first.Name.Contains("Allocation") && !first.Name.Contains("Join"))
                        {
                            chosenCtor = c;
                            break;
                        }
                        // exact match for JoinAllocation
                        if (relayAllocObject is JoinAllocation && first.Name.Contains("JoinAllocation"))
                        {
                            chosenCtor = c;
                            break;
                        }
                    }

                    if (chosenCtor != null)
                    {
                        object relayData = null;
                        try
                        {
                            relayData = chosenCtor.Invoke(new[] { relayAllocObject, "dtls" });
                        }
                        catch (Exception ce)
                        {
                            Debug.LogWarning($"[RelayManager] Constructing RelayServerData threw: {ce.Message}");
                            relayData = null;
                        }

                        if (relayData != null)
                        {
                            if (paramType.IsInstanceOfType(relayData))
                            {
                                method.Invoke(targetObj, new[] { relayData });
                                Debug.Log($"[RelayManager] Invoked {method.Name} with RelayServerData on {t.FullName}");
                                return true;
                            }
                            else
                            {
                                // Try anyway (some wrappers accept base object types)
                                try
                                {
                                    method.Invoke(targetObj, new[] { relayData });
                                    Debug.Log($"[RelayManager] Invoked {method.Name} with RelayServerData on {t.FullName} (param mismatch but invocation succeeded)");
                                    return true;
                                }
                                catch (Exception exTry)
                                {
                                    Debug.LogWarning($"[RelayManager] Invocation with RelayServerData failed: {exTry.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[RelayManager] No suitable RelayServerData constructor found that matches the allocation type.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayManager] Invocation failed for method {method.Name} on {t.FullName}: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Try to set an allocation/JoinAllocation on a writable field/property (fallback).
    /// </summary>
    private bool TrySetAllocationFieldOnObject(object targetObj, object relayAllocObject)
    {
        Type t = targetObj.GetType();

        var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     .Where(p => p.CanWrite && (p.PropertyType.Name.IndexOf("allocation", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("relay", StringComparison.OrdinalIgnoreCase) >= 0));
        foreach (var p in props)
        {
            try
            {
                if (p.PropertyType.IsInstanceOfType(relayAllocObject))
                {
                    p.SetValue(targetObj, relayAllocObject);
                    Debug.Log($"[RelayManager] Set property '{p.Name}' on {t.FullName} with allocation object.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayManager] Failed setting property '{p.Name}': {ex.Message}");
            }
        }

        var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                      .Where(f => (f.FieldType.Name.IndexOf("allocation", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("relay", StringComparison.OrdinalIgnoreCase) >= 0));
        foreach (var f in fields)
        {
            try
            {
                if (f.FieldType.IsInstanceOfType(relayAllocObject))
                {
                    f.SetValue(targetObj, relayAllocObject);
                    Debug.Log($"[RelayManager] Set field '{f.Name}' on {t.FullName} with allocation object.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayManager] Failed setting field '{f.Name}': {ex.Message}");
            }
        }

        return false;
    }

    private Type FindTypeByName(string partialName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in assemblies)
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }

            foreach (var t in types)
            {
                if (t.Name.Equals(partialName, StringComparison.OrdinalIgnoreCase) || (t.FullName != null && t.FullName.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0))
                    return t;
            }
        }
        return null;
    }

    #endregion
}
