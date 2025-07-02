using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemieAI : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;

    public LayerMask whatIsGround;
    public LayerMask whatIsPlayer;
    public bool isAgressive;
    public Vector3 walkPoint;
    bool walkPointSet;

    [Header("Patrol Settings")]
    public float walkPointRange = 10f;
    public float minWalkPointRange = 5f;

    [Header("Attack Settings")]
    public float timeBetweenAttacks = 2f;
    public int damage = 20;
    bool alreadyAttacked;
    public float jumpForce = 6f;

    [Header("Detection Settings")]
    public float sightRange = 20f;
    public float attackRange = 2f;
    public bool playerInSightRange, playerInAttackRange;

    private Animator animator;
    private bool hasDealtDamage;

    // For Jump Physics
    private Transform jumpPhysics;
    private Rigidbody jumpRb;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = FindObjectOfType<PlayerMovement>()?.transform;

        // Find the JumpPhysics child
        jumpPhysics = transform.Find("Wolf/Wolf/JumpPhysics");
        if (jumpPhysics != null)
            jumpRb = jumpPhysics.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (player == null || isJumping) return;

        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange)
            Patroling();
        else if (playerInSightRange && !playerInAttackRange && isAgressive)
            ChasePlayer();
        else if (playerInAttackRange &  isAgressive)
            Attack();

        UpdateAnimations();
    }

    private void Patroling()
    {
        if (!walkPointSet)
            SearchWalkPoint();

        if (walkPointSet)
            agent.SetDestination(walkPoint);

        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(walkPoint.x, 0, walkPoint.z)
        );

        if (distance < 1f)
            walkPointSet = false;
    }

    private void SearchWalkPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(-walkPointRange, walkPointRange);
            float z = Random.Range(-walkPointRange, walkPointRange);

            Vector3 potential = new Vector3(transform.position.x + x, transform.position.y + 5f, transform.position.z + z);

            if (Physics.Raycast(potential, Vector3.down, out RaycastHit hit, 10f, whatIsGround))
            {
                Vector3 flat = new Vector3(potential.x, hit.point.y, potential.z);
                if (Vector3.Distance(transform.position, flat) >= minWalkPointRange)
                {
                    walkPoint = flat;
                    walkPointSet = true;
                    return;
                }
            }
        }
    }

    private void ChasePlayer()
    {
        if (player != null)
            agent.SetDestination(player.position);
    }

    private void Attack()
    {
        if (!alreadyAttacked && player != null)
        {
            alreadyAttacked = true;
            hasDealtDamage = false;

            animator.SetTrigger("Jump");
            StartCoroutine(JumpSequence());

            Invoke(nameof(ResetAttack), timeBetweenAttacks + 1f);
        }
    }

    bool isJumping = false;

    IEnumerator JumpSequence()
    {
        isJumping = true;

        agent.enabled = false;
        if (jumpRb != null)
        {
            jumpRb.isKinematic = false;

            Vector3 direction = (player.position - transform.position).normalized;
            jumpRb.velocity = Vector3.zero;
            jumpRb.AddForce(direction * jumpForce + Vector3.up * 5f, ForceMode.VelocityChange);
        }

        yield return new WaitForSeconds(0.4f);
        TryDamagePlayer();

        yield return new WaitForSeconds(0.6f);

        if (jumpRb != null)
        {
            jumpRb.velocity = Vector3.zero;
            jumpRb.isKinematic = true;
        }

        // Sync this GameObject's position with JumpPhysics's position
        if (jumpPhysics != null)
            transform.position = jumpPhysics.position;

        agent.enabled = true;
        agent.SetDestination(player.position);

        isJumping = false;
    }

    private void TryDamagePlayer()
    {
        if (hasDealtDamage || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange + 1f)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage, transform.position);
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
        if (animator == null || agent == null) return;

        bool isMoving = agent.velocity.magnitude > 0.1f && agent.remainingDistance > agent.stoppingDistance;
        animator.SetBool("IsWalking", isMoving);
    }

    private void LateUpdate()
    {
        if (!agent.enabled || player == null || isJumping) return;

        Vector3 dir;

        if (playerInAttackRange)
            dir = (player.position - transform.position).normalized;
        else
            dir = agent.velocity.sqrMagnitude > 0.01f
                ? agent.velocity.normalized
                : (agent.steeringTarget - transform.position).normalized;

        dir.y = 0;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
