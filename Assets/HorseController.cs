using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class HorseController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float forwardSpeed = 5f;
    public float backwardSpeed = 2f;
    public float sprintMultiplier = 2f;
    public float turnSpeed = 100f;
    public float gravity = -9.81f;

    [HideInInspector] public bool isControlled = false;

    private Transform rider;
    private CharacterController characterController;
    private NavMeshAgent agent;
    private EnemieAI ai;
    private Animator animator;

    private Vector3 velocity;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        ai = GetComponent<EnemieAI>();
        animator = GetComponentInChildren<Animator>();
    }

    public void SetControlled(bool controlled, Transform riderTransform = null)
    {
        isControlled = controlled;
        rider = controlled ? riderTransform : null;

        if (agent != null)
            agent.enabled = !controlled;

        if (ai != null)
            ai.enabled = !controlled;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsGalloping", false);
        }
    }

    void Update()
    {
        if (!isControlled || rider == null) return;

        float moveInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");

        float moveSpeed = moveInput > 0 ? forwardSpeed : backwardSpeed;
        bool isSprinting = false;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed *= sprintMultiplier;
            isSprinting = true;
        }

        // Turning
        transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime);

        // Movement direction
        Vector3 move = transform.forward * moveInput * moveSpeed;

        // Gravity
        if (!characterController.isGrounded)
            velocity.y += gravity * Time.deltaTime;
        else
            velocity.y = -1f;

        move += velocity;

        characterController.Move(move * Time.deltaTime);

        // ✅ Update animations
        if (animator != null)
        {
            bool isMoving = Mathf.Abs(moveInput) > 0.1f;
            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsGalloping", isMoving && isSprinting);
        }
    }
}
