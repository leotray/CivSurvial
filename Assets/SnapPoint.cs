using UnityEngine;

public enum SnapDirection
{
    North,
    South,
    East,
    West,
    Up,
    Down
}

public class SnapPoint : MonoBehaviour
{
    public BuildableTypeNetworked allowedConnectionType;
    public SnapDirection direction;
}
