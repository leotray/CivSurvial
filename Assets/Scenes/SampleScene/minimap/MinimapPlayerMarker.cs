using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimapPlayerMarker : MonoBehaviour
{
    [Header("Marker Data")]
    public ulong playerId;
    public string playerName;
    public bool isLocalPlayer;

    [Header("Components")]
    private Image markerImage;
    private TextMeshProUGUI nameText;
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        markerImage = GetComponent<Image>();
        nameText = GetComponentInChildren<TextMeshProUGUI>();

        // FIXED: Ensure proper UI setup
        if (rectTransform == null)
        {
            Debug.LogError("[MinimapPlayerMarker] RectTransform component missing!");
        }

        // Ensure we don't block raycasts
        if (markerImage != null)
            markerImage.raycastTarget = false;
        if (nameText != null)
            nameText.raycastTarget = false;
    }

    public void Initialize(ulong id, string name, Color color, bool isLocal)
    {
        playerId = id;
        playerName = name;
        isLocalPlayer = isLocal;

        // FIXED: Set marker color
        if (markerImage != null)
            markerImage.color = color;
        else
            Debug.LogWarning("[MinimapPlayerMarker] MarkerImage is null!");

        UpdateName(name);

        // Scale local player slightly larger
        if (isLocal)
            transform.localScale = Vector3.one * 1.2f;
        else
            transform.localScale = Vector3.one;

        Debug.Log($"[MinimapPlayerMarker] Initialized marker for player {id} ({name}), isLocal: {isLocal}");
    }

    public void UpdatePosition(Vector2 position)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = position;
            Debug.Log($"[MinimapPlayerMarker] Updated position for player {playerId} to {position}");
        }
        else
        {
            Debug.LogError("[MinimapPlayerMarker] RectTransform is null when updating position!");
        }
    }

    public void UpdateName(string name)
    {
        playerName = name;
        if (nameText != null)
            nameText.text = name;
        else
            Debug.LogWarning("[MinimapPlayerMarker] NameText is null!");
    }
}
