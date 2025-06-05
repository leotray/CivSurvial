using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class playerMovement : MonoBehaviour
{
    public float currentSpeed;
    public float sprintSpeed;
    public float slowSpeed;
    public float normalSpeed;

    public Transform cameraTransform; // Assign your camera here in Inspector

    void Update()
    {
        Movement();
    }

    public void Movement()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

        // Get camera-based directions
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        // Flatten the directions to ignore vertical tilt
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Final move direction
        Vector3 moveDirection = camForward * input.z + camRight * input.x;

        // Choose speed
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = sprintSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftAlt))
        {
            currentSpeed = slowSpeed;
        }
        else
        {
            currentSpeed = normalSpeed;
        }

        // Apply movement
        transform.position += moveDirection * currentSpeed * Time.deltaTime;
    }
}
