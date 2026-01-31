using UnityEngine;

[System.Serializable]
public class LandChunk
{
    public Vector2Int coords;
    public bool isClaimed;
    public int owningFactionId; // ✅ faction ID instead of just string

    public LandChunk(Vector2Int coords)
    {
        this.coords = coords;
        this.isClaimed = false;
        this.owningFactionId = -1;
    }
}
