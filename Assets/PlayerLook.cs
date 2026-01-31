using UnityEngine;
using FishNet.Object;

[RequireComponent(typeof(Rigidbody))]
public class PlayerLook : NetworkBehaviour
{
    [Header("Look")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private Transform cameraTransform;

    private float xRotation = 0f;
    private Rigidbody rb;

    public override void OnStartClient()
    {
        base.OnStartClient();

        rb = GetComponent<Rigidbody>();
        if (!IsOwner)
        {
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) cam.gameObject.SetActive(false);
            }
            else
            {
                cameraTransform.gameObject.SetActive(false);
            }

            enabled = false;
            return;
        }

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>(true)?.transform;

        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        // Yaw on the Rigidbody using MoveRotation
        Quaternion deltaRotation = Quaternion.Euler(0f, mouseX, 0f);
        rb.MoveRotation(rb.rotation * deltaRotation);

        // Pitch on the camera locally (not synced)
        xRotation = Mathf.Clamp(xRotation - mouseY, -90f, 90f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
