using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementMultiplayer : NetworkBehaviour
{
    public float sprintSpeed = 10f;
    public float slowSpeed = 2f;
    public float normalSpeed = 5f;
    private float currentSpeed;
    public float cameraYOffset;
    public Transform glasses;


    public Transform cameraTransform; // Set at runtime

    private Rigidbody rb;
    private Vector3 moveDirection;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            // Assign the camera transform
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cameraTransform = cam.transform;
            }

            // Enable movement script
            movement moveScript = GetComponentInChildren<movement>(true);
            if (moveScript != null)
            {
                moveScript.enabled = true;
            }
        }
        else
        {
            // Disable camera for non-owners
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
            }

            // Disable movement script for non-owners
            movement moveScript = GetComponentInChildren<movement>(true);
            if (moveScript != null)
            {
                moveScript.enabled = false;
            }
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (!IsOwner) return;
        HandleInput();
        // Set position to match player every frame
        glasses.position = transform.position + new Vector3(0, 1.7f, 0); // Adjust Y to match head height
        glasses.rotation = Camera.main.transform.rotation; // Or however you're rotating it
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        MovePlayer();
    }

    void HandleInput()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * input.z + camRight * input.x).normalized;

        if (Input.GetKey(KeyCode.LeftShift))
            currentSpeed = sprintSpeed;
        else if (Input.GetKey(KeyCode.LeftAlt))
            currentSpeed = slowSpeed;
        else
            currentSpeed = normalSpeed;
    }

    void MovePlayer()
    {
        Vector3 targetPosition = rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPosition);
    }
}
