using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementMultiplayer : NetworkBehaviour
{
    public float sprintSpeed = 10f;
    public float slowSpeed = 2f;
    public float normalSpeed = 5f;
    private float currentSpeed;
    public float cameraYOffset;

    public Transform cameraTransform; // Assign in Inspector

    private Rigidbody rb;
    private Vector3 moveDirection;

    public void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            cameraTransform = Camera.main.transform;
            cameraTransform.position = new Vector3(transform.position.x, transform.position.y + cameraYOffset, transform.position.z);
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Prevent unwanted rotation from physics
        rb.interpolation = RigidbodyInterpolation.Interpolate; // ADD THIS

    }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        MovePlayer();
    }

    void HandleInput()
    {
        // Camera-relative input
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        moveDirection = (camForward * input.z + camRight * input.x).normalized;

        // Speed selection
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
