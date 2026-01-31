using UnityEngine;
using static Item;

public enum TargetType
{
    Tree,
    Stone,
    Dirt
}

public class MineableObject : MonoBehaviour
{
    public TargetType targetType;
    public int maxHealth = 100;
    public GameObject dropPrefab; // 👈 Assign in Inspector
    public int dropAmount = 1;    // Optional: How many items to drop

    public int health;

    void Start()
    {
        health = maxHealth;
    }

    public void takeDamage(int damage)
    {
        health -= damage;
        Debug.Log($"{name} took {damage} damage. Remaining: {health}");

        if (health <= 0)
        {
            die();
        }
    }

    void die()
    {
        Debug.Log($"{name} died!");

        // 💥 Drop item(s)
        DropItems();

        Destroy(gameObject);
    }

    void DropItems()
    {
        if (dropPrefab == null) return;

        for (int i = 0; i < dropAmount; i++)
        {
            Vector3 dropPosition = transform.position + Random.insideUnitSphere * 0.5f;
            dropPosition.y = transform.position.y;
            Instantiate(dropPrefab, dropPosition, Quaternion.identity);
        }
    }
}
