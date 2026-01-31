using UnityEngine;

public class PlayerChunkTracker : MonoBehaviour
{
    private Vector2Int currentChunk;

    private void Start()
    {
        currentChunk = LandChunkManager.Instance.GetChunkCoords(transform.position);
        Debug.Log($"[PlayerChunkTracker] Starting in chunk {currentChunk}");
    }

    private void Update()
    {
        Vector2Int newChunk = LandChunkManager.Instance.GetChunkCoords(transform.position);

        if (newChunk != currentChunk)
        {
            currentChunk = newChunk;
            LandChunk chunk = LandChunkManager.Instance.GetChunk(currentChunk);

            if (chunk != null)
            {
                if (chunk.isClaimed)
                    Debug.Log($"[PlayerChunkTracker] Entered chunk {currentChunk}, claimed by faction {chunk.owningFactionId}");
                else
                    Debug.Log($"[PlayerChunkTracker] Entered chunk {currentChunk}, unclaimed");
            }
        }
    }

}
