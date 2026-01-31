using UnityEngine;

public class ChunkDebugVisualizer : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.G; // Toggle key like F3+G in Minecraft
    private bool showBorders = false;
    private LandChunkManager chunkManager;

    private void Start()
    {
        chunkManager = FindObjectOfType<LandChunkManager>();

        if (chunkManager == null)
            Debug.LogError("[ChunkDebugVisualizer] Could not find LandChunkManager in scene.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showBorders = !showBorders;
            Debug.Log($"[ChunkDebugVisualizer] Toggled chunk borders: {showBorders}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showBorders || chunkManager == null)
            return;

        // Use THIS object’s transform (the player) instead of PlayerRole
        Vector3 playerPos = transform.position;
        Vector2Int chunkCoord = chunkManager.GetChunkCoords(playerPos);
        float chunkSize = chunkManager.chunkSize;

        // Debug info
        Debug.Log($"[ChunkDebugVisualizer] Drawing borders for chunk {chunkCoord} at playerPos={playerPos}");

        // Calculate world position of the chunk’s bottom-left corner
        Vector3 origin = new Vector3(chunkCoord.x * chunkSize, 0, chunkCoord.y * chunkSize);

        Gizmos.color = Color.yellow;

        // Draw ground square
        Vector3 p1 = origin;
        Vector3 p2 = origin + new Vector3(chunkSize, 0, 0);
        Vector3 p3 = origin + new Vector3(chunkSize, 0, chunkSize);
        Vector3 p4 = origin + new Vector3(0, 0, chunkSize);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        // Vertical lines (to make it like Minecraft)
        float height = 50f;
        Gizmos.DrawLine(p1, p1 + Vector3.up * height);
        Gizmos.DrawLine(p2, p2 + Vector3.up * height);
        Gizmos.DrawLine(p3, p3 + Vector3.up * height);
        Gizmos.DrawLine(p4, p4 + Vector3.up * height);
    }
}
