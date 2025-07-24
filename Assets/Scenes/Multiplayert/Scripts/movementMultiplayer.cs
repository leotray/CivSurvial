using UnityEngine;

public class movementMultiplayer : MonoBehaviour
{
    public float sensitivity = 2f;
    public float slowSpeed = 2f;
    public float normalSpeed = 5f;
    public float sprintSpeed = 8f;
    float currentSpeed;
    public Transform playerTransform;

    void Start()
    {
        // Lock cursor at start
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Rotation();

        // (Optional movement logic here)
    }

    public void Rotation()
    {
        Vector3 mouseInput = new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0);
        transform.Rotate(mouseInput * sensitivity);
        Vector3 eulerRotation = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, 0);
    }
}
