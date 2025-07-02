using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemieAI : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;

    public LayerMask whatIsGround;
    public LayerMask whatIsPlayer;

    public Vector3 walkPoint;
    bool walkPointSet;

    [Header("Patrol Settings")]
    public float walkPointRange = 10f;
    public float minWalkPointRange = 5f;

    [Header("Attack Settings")]
    public float timeBetweenAttacks = 2f;
    public int damage = 20;
    bool alreadyAttacked;
    public float jumpForce = 5f;

    [Header("Detection Settings")]
    public float sightRange = 20f;
    public float attackRange = 2f;
    public bool playerInSightRange, playerInAttackRange;

    private Animator animator;
    private Rigidbody rb;
    private bool hasDealtDamage;

    private void Awake()
    {
        player = FindObjectOfType<PlayerMovement>()?.transform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        agent.updateRotation = false;
    }

    private void Update()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange)
            Patroling();
        else if (playerInSightRange && !playerInAttackRange)
            ChasePlayer();
        else if (playerInAttackRange)
            Attack();

        UpdateAnimations();
    }

    private void Patroling()
    {
        if (!walkPointSet)
            SearchWalkPoint();

        if (walkPointSet)
        {
            Vector3 direction = (walkPoint - transform.position).normalized;
            direction.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360 * Time.deltaTime);

            float angleDiff = Quaternion.Angle(transform.rotation, targetRotation);
            if (angleDiff < 5f)
                agent.SetDestination(walkPoint);
            else
                agent.SetDestination(transform.position);
        }

        Vector3 flatEnemyPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatWalkPoint = new Vector3(walkPoint.x, 0, walkPoint.z);
        float distance = Vector3.Distance(flatEnemyPos, flatWalkPoint);

        if (distance < 1f)
            walkPointSet = false;
    }

    private void SearchWalkPoint()
    {
        int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float randomZ = Random.Range(-walkPointRange, walkPointRange);
            float randomX = Random.Range(-walkPointRange, walkPointRange);

            Vector3 potentialPoint = new Vector3(
                transform.position.x + randomX,
                transform.position.y,
                transform.position.z + randomZ
            );

            float flatDistance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(potentialPoint.x, potentialPoint.z)
            );

            if (flatDistance < minWalkPointRange) continue;

            if (Physics.Raycast(potentialPoint, Vector3.down, 2f, whatIsGround))
            {
                walkPoint = potentialPoint;
                walkPointSet = true;
                break;
            }
        }
    }

    private void ChasePlayer()
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360 * Time.deltaTime);

            float angleDiff = Quaternion.Angle(transform.rotation, targetRotation);
            if (angleDiff < 5f)
                agent.SetDestination(player.position);
            else
                agent.SetDestination(transform.position);
        }
    }

    private void Attack()
    {
        if (!alreadyAttacked && player != null)
        {
            alreadyAttacked = true;
            agent.isStopped = true;
            hasDealtDamage = false;

            Vector3 toPlayer = (player.position - transform.position).normalized;
            Vector3 forwardTarget = player.position - toPlayer * 1.5f;

            StartCoroutine(DoJumpSequence(forwardTarget, toPlayer));

            Invoke(nameof(ResetAttack), timeBetweenAttacks + 1.2f);
        }
    }

    IEnumerator DoJumpSequence(Vector3 forwardTarget, Vector3 directionToPlayer)
    {
        agent.enabled = false;
        rb.isKinematic = false;
        animator?.SetTrigger("Jump");
        if (animator != null) animator.applyRootMotion = false;

        yield return new WaitForSeconds(0.25f); // prep time

        rb.velocity = Vector3.zero;
        Vector3 forwardJump = (forwardTarget - transform.position).normalized;
        rb.AddForce(forwardJump * jumpForce + Vector3.up * 3f, ForceMode.VelocityChange);

        // Wait and deal damage mid-air or just before land
        yield return new WaitForSeconds(0.3f);

        TryDamagePlayer();

        yield return new WaitForSeconds(0.3f);

        rb.velocity = Vector3.zero;

        // Retreat
        Vector3 retreatTarget = transform.position - directionToPlayer * 8f;
        Vector3 retreatDir = (retreatTarget - transform.position).normalized;
        rb.AddForce(retreatDir * jumpForce + Vector3.up * 3f, ForceMode.VelocityChange);

        yield return new WaitForSeconds(0.6f); // land

        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        agent.enabled = true;
        agent.isStopped = false;

        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, whatIsGround))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        if (animator != null) animator.applyRootMotion = true;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= sightRange)
            agent.SetDestination(player.position);
        else
            walkPointSet = false;
    }

    private void TryDamagePlayer()
    {
        if (hasDealtDamage || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= attackRange + 1f)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage);
                hasDealtDamage = true;
            }
        }
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    private void UpdateAnimations()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f && agent.remainingDistance > agent.stoppingDistance;
        animator?.SetBool("IsWalking", isMoving);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
