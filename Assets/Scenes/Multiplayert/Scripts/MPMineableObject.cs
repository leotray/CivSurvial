using UnityEngine;
using FishNet.Object;
using static Item;

public class MPMineableObject : NetworkBehaviour
{
    public TargetType targetType;
    public int maxHealth = 100;
    public GameObject dropPrefab;
    public int dropAmount = 1;

    public int health;

    void Start()
    {
        health = maxHealth;
    }

    public void takeDamage(int damage)
    {
        if (!IsServer) return; // Only the server should process damage

        health -= damage;
        Debug.Log($"{name} took {damage} damage. Remaining: {health}");

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{name} died!");

        if (TryGetComponent(out Banner banner))
        {
            banner.OnDestroyed(); // 👈 call Banner’s method
        }

        DropItems();

        base.Despawn(); // 👈 Despawn it instead of Destroy()
    }

    void DropItems()
    {
        if (dropPrefab == null) return;

        for (int i = 0; i < dropAmount; i++)
        {
            Vector3 dropPosition = transform.position + Random.insideUnitSphere * 0.5f;
            dropPosition.y = transform.position.y;

            // You MUST spawn the drops as NetworkObjects too
            GameObject drop = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
            if (drop.TryGetComponent(out NetworkObject netObj))
            {
                base.Spawn(netObj);
            }
            else
            {
                Debug.LogWarning("Drop prefab does not have a NetworkObject!");
            }
        }
    }

    void StartCapturing()
    {
        Debug.Log("Capturing started");
    }
}
