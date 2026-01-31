using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TMPro;

public class FactionTechUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public RectTransform contentParent;      // where to instantiate node prefabs (RectTransform)
    public GameObject techButtonPrefab;      // prefab that contains TechButtonMulti
    public TextMeshProUGUI pointsText;

    [Header("Local DB (client)")]
    public TechDatabase localTechDatabase; // assign same TechDatabase as server (client copy)

    [Header("Layout settings")]
    public float columnWidth = 300f;
    public float rowHeight = 180f;
    public float nodeWidth = 220f;
    public float nodeHeight = 120f;
    public Vector2 padding = new Vector2(50f, 50f);

    // internal
    private int currentFactionId = -1;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (contentParent == null)
            Debug.LogWarning("[FactionTechUI] contentParent not assigned (RectTransform).");
        if (techButtonPrefab == null)
            Debug.LogWarning("[FactionTechUI] techButtonPrefab not assigned.");
    }

    /// <summary>
    /// Called from FactionManager.TargetOpenFactionTechTree on the client.
    /// Signature kept same as before for compatibility.
    /// </summary>
    public void Populate(int factionId, string[] allTechIds, string[] unlockedTechIds, string[] knownRecipeNames, int techPoints, bool isLeader)
    {
        currentFactionId = factionId;

        if (pointsText != null)
            pointsText.text = $"Research Points: {techPoints}";

        // clear existing nodes
        if (contentParent != null)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(contentParent.GetChild(i).gameObject);
            }
        }

        HashSet<string> unlocked = new HashSet<string>(unlockedTechIds ?? new string[0]);

        // Build map id -> MPtech (if available)
        var techMap = new Dictionary<string, object>();
        if (localTechDatabase != null && allTechIds != null)
        {
            foreach (var id in allTechIds)
            {
                var t = localTechDatabase.GetById(id); // assumes GetById returns your MPtech object
                if (t != null)
                    techMap[id] = t;
                else
                    techMap[id] = null;
            }
        }

        // Build prerequisite graph (try reflection to find common fields/props)
        var prereqMap = new Dictionary<string, List<string>>();
        foreach (var id in allTechIds ?? new string[0])
        {
            List<string> prereqs = new List<string>();
            if (techMap.ContainsKey(id) && techMap[id] != null)
            {
                var techObj = techMap[id];
                var list = TryGetPrereqIds(techObj);
                if (list != null && list.Length > 0)
                    prereqs.AddRange(list.Where(s => !string.IsNullOrEmpty(s)));
            }
            // if no DB info found, treat as root (empty prerequisites)
            prereqMap[id] = prereqs;
        }

        // Compute depth for every tech (depth = max depth of prereqs + 1; roots = 0)
        var depthMemo = new Dictionary<string, int>();
        var visiting = new HashSet<string>();
        int GetDepth(string nodeId)
        {
            if (depthMemo.TryGetValue(nodeId, out int d)) return d;
            if (!prereqMap.ContainsKey(nodeId) || prereqMap[nodeId].Count == 0)
            {
                depthMemo[nodeId] = 0;
                return 0;
            }

            if (visiting.Contains(nodeId))
            {
                Debug.LogWarning($"[FactionTechUI] Detected cycle in prerequisites at {nodeId}. Treating as root.");
                depthMemo[nodeId] = 0;
                return 0;
            }

            visiting.Add(nodeId);
            int max = 0;
            foreach (var p in prereqMap[nodeId])
            {
                if (!prereqMap.ContainsKey(p))
                {
                    // prerequisite not in the provided allTechIds list — treat as root
                    continue;
                }

                int pd = GetDepth(p);
                if (pd + 1 > max) max = pd + 1;
            }
            visiting.Remove(nodeId);
            depthMemo[nodeId] = max;
            return max;
        }

        foreach (var id in allTechIds ?? new string[0])
            GetDepth(id);

        // Group by depth
        var groups = new Dictionary<int, List<string>>();
        int maxDepth = 0;
        foreach (var kv in depthMemo)
        {
            int d = kv.Value;
            if (!groups.ContainsKey(d)) groups[d] = new List<string>();
            groups[d].Add(kv.Key);
            if (d > maxDepth) maxDepth = d;
        }

        // Determine max column height to size content rect
        int maxColumnCount = groups.Count == 0 ? 0 : groups.Max(g => g.Value.Count);

        float contentWidth = padding.x * 2f + (maxDepth + 1) * columnWidth;
        float contentHeight = padding.y * 2f + Math.Max(1, maxColumnCount) * rowHeight;

        if (contentParent != null)
        {
            // set content size (assumes pivot/anchored appropriate; contentParent anchored to top-left recommended)
            contentParent.sizeDelta = new Vector2(contentWidth, contentHeight);
        }

        // For each depth column, center nodes vertically and instantiate
        foreach (var kv in groups)
        {
            int depth = kv.Key;
            var list = kv.Value;
            // Optionally sort the list to stable order (by name)
            list.Sort(StringComparer.Ordinal);

            // compute start Y to center the column
            float columnMidY = padding.y + (contentHeight - padding.y * 2f) / 2f;
            float totalGroupHeight = (list.Count - 1) * rowHeight;
            float startOffsetFromTop = columnMidY - (totalGroupHeight / 2f);

            for (int i = 0; i < list.Count; i++)
            {
                string techId = list[i];
                object techObj = techMap.ContainsKey(techId) ? techMap[techId] : null;
                bool isUnlocked = unlocked.Contains(techId);

                // instantiate prefab
                if (contentParent == null || techButtonPrefab == null) continue;

                GameObject go = Instantiate(techButtonPrefab, contentParent);
                go.name = $"TechNode_{techId}";

                // ensure RectTransform anchor/pivot top-left for consistent positioning
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) rt = go.AddComponent<RectTransform>();

                // set size and anchors/pivots for top-left based layout
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(nodeWidth, nodeHeight);

                float posX = padding.x + depth * columnWidth;
                float posY = - (padding.y + (startOffsetFromTop - padding.y) + i * rowHeight);

                rt.anchoredPosition = new Vector2(posX, posY);

                // Initialize the button component (TechButtonMulti) - keep same call you used previously
                var tb = go.GetComponent<TechButtonMulti>();
                if (tb != null)
                {
                    // If local DB object is of your MPtech type, pass it; otherwise tb.Init should handle null gracefully
                    try
                    {
                        tb.Init(techObj as MPtech, isUnlocked, isLeader, techPoints, factionId);
                    }
                    catch (Exception ex)
                    {
                        // Fallback: if your TechButtonMulti expects a specific type, try to call with reflection fallback
                        Debug.LogWarning("[FactionTechUI] TechButtonMulti.Init call failed. Make sure signature matches. " + ex.Message);
                        // If the tb.Init signature is different, you'll need to adjust or wrap accordingly.
                    }
                }
                else
                {
                    // If no TechButtonMulti found, try a component with another name or log
                    Debug.LogWarning($"[FactionTechUI] techButtonPrefab missing TechButtonMulti component for tech {techId}.");
                }

                // Optional: disable interactability on placeholder nodes when no DB entry
                if (techObj == null)
                {
                    var canvasGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                    canvasGroup.interactable = false;
                    canvasGroup.alpha = 0.6f;
                }
            }
        }

        if (panel != null) panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        currentFactionId = -1;
    }

    public bool IsOpen()
    {
        return panel != null && panel.activeSelf;
    }

    /// <summary>
    /// Tries to extract prerequisite ids from your MPtech object using reflection to be flexible across DB shapes.
    /// Looks for common field/property names and formats (string[], List<string>, comma-separated string).
    /// Returns null if none found.
    /// </summary>
    private string[] TryGetPrereqIds(object techObj)
    {
        if (techObj == null) return null;

        // candidate names - extend if your MPtech uses different names
        string[] candidateNames = new string[]
        {
            "prerequisites", "prereq", "prereqIds", "prerequisiteIds", "requiredTechs", "requires", "parents", "dependencies", "prerequisite"
        };

        Type t = techObj.GetType();
        foreach (var name in candidateNames)
        {
            // try property
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var val = prop.GetValue(techObj);
                var arr = ConvertToStringArray(val);
                if (arr != null) return arr;
            }

            // try field
            var field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var val = field.GetValue(techObj);
                var arr = ConvertToStringArray(val);
                if (arr != null) return arr;
            }
        }

        // nothing found
        return null;
    }

    private string[] ConvertToStringArray(object value)
    {
        if (value == null) return null;

        // string[]
        if (value is string[] sa) return sa;
        // List<string>
        var ienum = value as System.Collections.IEnumerable;
        if (ienum != null)
        {
            var list = new List<string>();
            bool any = false;
            foreach (var x in ienum)
            {
                any = true;
                if (x != null) list.Add(x.ToString());
            }
            if (any) return list.ToArray();
        }
        // comma-separated string
        if (value is string s)
        {
            if (s.Contains(",")) return s.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            if (!string.IsNullOrWhiteSpace(s)) return new[] { s.Trim() };
        }

        return null;
    }
}
