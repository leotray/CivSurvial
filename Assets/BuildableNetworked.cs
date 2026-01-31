using UnityEngine;
using FishNet.Object;

public enum BuildableTypeNetworked
{
    Floor,
    Wall,
    Stairs,
    Foundation
}

[RequireComponent(typeof(NetworkObject))]
public class BuildableNetworked : NetworkBehaviour
{
    [Header("Buildable Settings")]
    public BuildableTypeNetworked buildableType;

    [Tooltip("Optional: vertical offset for stacking (used if no snap point is found)")]
    public float verticalSnapOffset = 1f;

    [Header("Snap Points")]
    public SnapPoint[] snapPoints;

    private void Awake()
    {
        if (snapPoints == null || snapPoints.Length == 0)
            snapPoints = GetComponentsInChildren<SnapPoint>(true);
    }

    public SnapPoint[] GetSnapPoints()
    {
        return snapPoints;
    }
}
