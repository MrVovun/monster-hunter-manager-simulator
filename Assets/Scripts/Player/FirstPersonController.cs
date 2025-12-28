using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpForce = 5.5f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private InvestigationManager investigationManager;
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalRotation = 0f;
    private bool isMovementLocked = false;
    private float verticalVelocity = 0f;
    
    private void Awake()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
        
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }
        }
        
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void Update()
    {
        if (isMovementLocked) return;
        
        // Always read keyboard fallback to clear stale input when actions aren't wired
        ReadInputFallback();
        
        HandleMovement();
        HandleLook();
        
        // Reset inputs after processing to avoid sticky values when callbacks aren't firing
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        Vector3 horizontal = moveDirection * walkSpeed;

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // keep grounded
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);
        characterController.Move(finalVelocity * Time.deltaTime);
    }
    
    private void HandleLook()
    {
        // Horizontal rotation (Y-axis)
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
        
        // Vertical rotation (X-axis) - clamped to prevent over-rotation
        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
    
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnBestiary(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        var manager = ResolveInvestigationManager();
        manager?.ShowBestiaryFree();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed || isMovementLocked) return;
        if (characterController != null && characterController.isGrounded)
        {
            verticalVelocity = jumpForce;
        }
    }
    
    // Always read keyboard fallback (works even if Input Actions aren't connected)
    private void ReadInputFallback()
    {
        if (Keyboard.current != null)
        {
            Vector2 move = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) move.y += 1;
            if (Keyboard.current.sKey.isPressed) move.y -= 1;
            if (Keyboard.current.aKey.isPressed) move.x -= 1;
            if (Keyboard.current.dKey.isPressed) move.x += 1;
            moveInput = move;

            if (Keyboard.current.spaceKey.wasPressedThisFrame && characterController != null && characterController.isGrounded)
            {
                verticalVelocity = jumpForce;
            }
        }

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            lookInput += mouseDelta * mouseSensitivity * 0.01f; // Accumulate mouse delta
        }
    }
    
    public void LockMovement()
    {
        isMovementLocked = true;
        moveInput = Vector2.zero;
    }
    
    public void UnlockMovement()
    {
        isMovementLocked = false;
    }
    
    public bool IsMovementLocked()
    {
        return isMovementLocked;
    }

    public Camera GetPlayerCamera()
    {
        if (cameraTransform == null) return null;
        return cameraTransform.GetComponent<Camera>();
    }

    private InvestigationManager ResolveInvestigationManager()
    {
        if (investigationManager != null) return investigationManager;
        return GameManager.Instance != null ? GameManager.Instance.GetInvestigationManager() : null;
    }
}
