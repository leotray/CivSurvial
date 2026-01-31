using System.Collections.Generic;
using UnityEngine;

public class LandChunkManager : MonoBehaviour
{
    public static LandChunkManager Instance;

    [Header("Chunk Settings")]
    public int chunkSize = 20; // Each chunk covers 20x20 world units

    private Dictionary<Vector2Int, LandChunk> chunks = new Dictionary<Vector2Int, LandChunk>();
    private int worldWidth;
    private int worldDepth;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Called by TerrainGeneratorMultiplayer after terrain is generated
    public void InitializeChunks(int width, int depth)
    {
        worldWidth = width;
        worldDepth = depth;

        chunks.Clear();

        int chunksX = Mathf.CeilToInt((float)width / chunkSize);
        int chunksZ = Mathf.CeilToInt((float)depth / chunkSize);

        for (int x = 0; x < chunksX; x++)
        {
            for (int z = 0; z < chunksZ; z++)
            {
                Vector2Int coords = new Vector2Int(x, z);
                chunks[coords] = new LandChunk(coords);
            }
        }

        Debug.Log($"[LandChunkManager] Created {chunks.Count} chunks ({chunksX}x{chunksZ})");
    }

    // Get the chunk coordinates from a world position
    public Vector2Int GetChunkCoords(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / chunkSize);
        int z = Mathf.FloorToInt(worldPos.z / chunkSize);
        return new Vector2Int(x, z);
    }

    public LandChunk GetChunkAtWorldPos(Vector3 worldPos)
    {
        Vector2Int coords = GetChunkCoords(worldPos);
        chunks.TryGetValue(coords, out LandChunk chunk);
        return chunk;
    }
    public bool ClaimChunk(Vector2Int coords, int factionId)
    {
        if (!chunks.TryGetValue(coords, out LandChunk chunk))
            return false;

        if (chunk.isClaimed)
        {
            Debug.LogWarning($"[LandChunkManager] Chunk {coords} already claimed by faction {chunk.owningFactionId}");
            return false;
        }

        chunk.isClaimed = true;
        chunk.owningFactionId = factionId;
        Debug.Log($"[LandChunkManager] Chunk {coords} claimed for faction {factionId}");
        return true;
    }

    public LandChunk GetChunk(Vector2Int coords)
    {
        chunks.TryGetValue(coords, out LandChunk chunk);
        return chunk;
    }

}
