using UnityEngine;
using FishNet.Object;

public class ThrowableProjectile : NetworkBehaviour
{
    public int damage;
    public float lifetime = 5f;
    public int ownerId;   // <-- use int, matches FishNet ClientId

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    [Server]
    private void OnCollisionEnter(Collision collision)
    {
        var stats = collision.gameObject.GetComponent<PlayerStatsNetworked>();

        // Prevent hitting yourself
        if (stats != null && stats.Owner.ClientId == ownerId)
            return;

        // Damage player
        if (stats != null)
        {
            stats.TakeDamage(damage, transform.position, false);
            Despawn();
            return;
        }

        // Damage mineable
        var mineable = collision.gameObject.GetComponent<MPMineableObject>();
        if (mineable != null)
        {
            mineable.takeDamage(damage);
            Despawn();
        }
    }

    [Server]
    private void Despawn()
    {
        base.Despawn(gameObject); // FishNet despawn
    }
}
