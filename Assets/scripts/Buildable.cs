using UnityEngine;

public enum BuildableType
{
    Floor,
    Wall,
    Stairs,
    Foundation
}

public class Buildable : MonoBehaviour
{
    public BuildableType buildableType;
    public float verticalSnapOffset = 1f; // Half height of object for stacking
}

