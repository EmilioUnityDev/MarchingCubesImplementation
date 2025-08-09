using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private bool mvHorizontal;
    private bool mvForwardBackward;
    private float horizontalInput;
    private float forwardBackwardInput;

    private Rigidbody rb;

    [SerializeField]
    private bool isGrounded = true;

    [SerializeField]
    private float moveSpeed = 2f; // Speed at which the player moves
    [SerializeField]
    private float jumpForce = 2f; // Force applied when the player jumps
    [SerializeField]
    private float gravityScale = 1f; // Scale for the gravity effect

    [SerializeField]
    private Camera playerCamera; // Reference to the player's camera
    [SerializeField, Tooltip("Camera distance from the player"), Range(0.5f, 5f)]
    private float cameraDistance = 3f; // Distance of the camera from the player

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogError("Rigidbody component not found on the player object.");
    }

    private void Update()
    {
        // Apply mouse movement to the camera
        if (playerCamera != null)
        {
            // Lock the cursor to the center of the screen and make it invisible
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Rotate the camera based on mouse input
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * 0.1f; // Adjust sensitivity as needed
            float mouseY = mouseDelta.y * 0.1f; // Adjust sensitivity as needed

            // Rotate the player object around the Y-axis
            transform.Rotate(Vector3.up, mouseX);

            // Rotate the camera around the X-axis
            playerCamera.transform.Rotate(Vector3.left, mouseY);

            // Clamp the camera's vertical rotation to prevent flipping [270 - 360] and [0 - 90]
            Vector3 cameraRotation = new(playerCamera.transform.localEulerAngles.x, 0, 0);
            cameraRotation.x = Mathf.Clamp((cameraRotation.x + 90) % 360, 0f, 180f) - 90f;
            playerCamera.transform.localEulerAngles = cameraRotation;

            // In this part we prevent the camera from clipping through the terrain
            Vector3 cameraPosition = transform.position - playerCamera.transform.forward * cameraDistance; // Set the camera position behind the player
            Vector3 rayOrigin = transform.position; // Start ray from the player's position
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.Normalize(cameraPosition - rayOrigin), out hit, 5f, LayerMask.GetMask("Terrain")))
            {
                // If the ray hits the terrain, set the camera position to the hit point or the camera distance if the hit point is too far
                if (hit.distance < cameraDistance)
                {
                    playerCamera.transform.position = hit.point;
                }
                else
                {
                    playerCamera.transform.position = cameraPosition;
                }
            }
            else
            {
                // If nothing is hit, set the camera position to a default distance
                playerCamera.transform.position = cameraPosition;
            }
        }
        else
        {
            Debug.LogError("Player camera not assigned.");
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
        ApplyGravity();
    }

    private void MovePlayer()
    {
        Vector3 direction = new Vector3(horizontalInput * transform.right.x + forwardBackwardInput * transform.forward.x, 0,
                                         horizontalInput * transform.right.z + forwardBackwardInput * transform.forward.z).normalized;
        Vector3 force = direction * moveSpeed - new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        // Apply force to the rigidbody for movement
        if (mvHorizontal || mvForwardBackward)
        {
            rb.AddForce(force, ForceMode.VelocityChange);
        }

        // If we aren't moving and on the ground, stop velocity so we don't slide
        if (!mvHorizontal && !mvForwardBackward && isGrounded)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    private void ApplyGravity()
    {
        // Apply gravity manually
        if (rb.linearVelocity.y >= 0)
        {
            // If the player is moving upwards, apply a downward force to simulate gravity
            rb.AddForce(Physics.gravity * 2 * rb.mass * gravityScale);
        }
        else if (rb.linearVelocity.y < 0)
        {
            // If the player is moving downwards, apply a downward force to simulate gravity
            rb.AddForce(Physics.gravity * 2.5f * rb.mass * gravityScale);
        }
    }

    public void Jump()
    {
        Debug.Log("Jump");
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    public void Move(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Vector3 input = context.ReadValue<Vector3>();
            Debug.Log($"Move input: {input}");
            mvHorizontal = input.x != 0;
            mvForwardBackward = input.z != 0;
            horizontalInput = input.x;
            forwardBackwardInput = input.z;
        }
        else if (context.canceled)
        {
            mvHorizontal = false;
            mvForwardBackward = false;
        }
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log($"Shoot action performed with: " + context.ReadValue<float>());

            // Calculate the hit point based on the camera's forward direction, starting from the player's position
            Ray ray = new Ray(transform.position, playerCamera.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, LayerMask.GetMask("Terrain")))
            {
                Debug.Log($"Hit point: {hit.point}");
                // Call the method to terraform the terrain at the hit point
                MeshGenerator.Instance.Terraform(context.ReadValue<float>(), hit.point);
            }
        }
    }
}
