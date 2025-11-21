using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalRotation = 0f;
    private bool isMovementLocked = false;
    
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
        
        // Try to read input if callbacks aren't set up (fallback)
        ReadInput();
        
        HandleMovement();
        HandleLook();
        
        // Reset look input after processing (for mouse delta)
        lookInput = Vector2.zero;
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        moveDirection = moveDirection.normalized * walkSpeed;
        
        // Apply gravity
        moveDirection.y = -9.81f;
        
        characterController.Move(moveDirection * Time.deltaTime);
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
    
    // Alternative: Read directly from Input System (fallback if Input Actions aren't connected)
    private void ReadInput()
    {
        // Only use fallback if Input Actions callbacks haven't set values
        // This allows both systems to work
        if (Keyboard.current != null)
        {
            Vector2 move = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) move.y += 1;
            if (Keyboard.current.sKey.isPressed) move.y -= 1;
            if (Keyboard.current.aKey.isPressed) move.x -= 1;
            if (Keyboard.current.dKey.isPressed) move.x += 1;
            
            // Only override if no callback value was set
            if (moveInput == Vector2.zero && move != Vector2.zero)
            {
                moveInput = move;
            }
        }
        
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (mouseDelta != Vector2.zero)
            {
                lookInput += mouseDelta * mouseSensitivity * 0.01f; // Accumulate mouse delta
            }
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
}

