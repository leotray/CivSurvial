using UnityEngine;
using UnityEngine.UI;

public class MinimapStructureMarker : MonoBehaviour
{
    
    public GameObject targetStructure { get; private set; }
    public string structureName { get; private set; }

    [Header("Components")]
    private Image markerImage;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        markerImage = GetComponent<Image>();

        // Ensure we don't block raycasts
        if (markerImage != null)
            markerImage.raycastTarget = false;
    }

    public void Initialize(GameObject structure, string name, Sprite icon)
    {
        targetStructure = structure;
        structureName = name;

        if (markerImage != null && icon != null)
            markerImage.sprite = icon;

        Debug.Log($"[MinimapStructureMarker] Initialized structure marker for {name}");
    }

    // FIXED: Simple direct positioning like player markers
    public void UpdatePosition(Vector2 minimapPosition)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = minimapPosition;
            Debug.Log($"[MinimapStructureMarker] Updated position for {structureName} to {minimapPosition}");
        }
        else
        {
            Debug.LogError("[MinimapStructureMarker] RectTransform is null when updating position!");
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (markerImage != null && icon != null)
            markerImage.sprite = icon;
    }

    public void SetColor(Color color)
    {
        if (markerImage != null)
            markerImage.color = color;
    }
}
