using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isMoving;
    private Vector3 lastPosition;

    public bool IsMoving => isMoving;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("PlayerMovement requires a CharacterController.", this);
            enabled = false;
            return;
        }

        EnsureGroundCheck();

        if (groundMask.value == 0)
        {
            groundMask = ~(1 << gameObject.layer);
        }
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        CheckGround();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        CheckMovementState();
    }

    private void EnsureGroundCheck()
    {
        if (groundCheck != null)
        {
            return;
        }

        GameObject groundCheckObject = new GameObject("GroundCheck");
        groundCheckObject.transform.SetParent(transform, false);

        float footOffset = Mathf.Max(0.05f, controller.height * 0.5f - controller.radius + 0.05f);
        groundCheckObject.transform.localPosition = new Vector3(0f, -footOffset, 0f);
        groundCheck = groundCheckObject.transform;
    }

    private void CheckGround()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void CheckMovementState()
    {
        isMoving = isGrounded && lastPosition != transform.position;
        lastPosition = transform.position;
    }
}
