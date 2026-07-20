using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 5f;
    [Min(0f)]
    [SerializeField] private float sprintSpeed = 8f;
    [Min(0f)]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Movement Feel")]
    [Min(0f)]
    [SerializeField] private float groundAcceleration = 35f;
    [Min(0f)]
    [SerializeField] private float groundDeceleration = 45f;
    [Min(0f)]
    [SerializeField] private float airAcceleration = 14f;
    [Min(0f)]
    [SerializeField] private float airDeceleration = 2f;

    [Header("Jump Forgiveness")]
    [Min(0f)]
    [SerializeField] private float coyoteTime = 0.12f;
    [Min(0f)]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float maxFallSpeed = -35f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [Min(0.01f)]
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;
    [Min(0.01f)]
    [SerializeField] private float groundNormalProbeDistance = 0.35f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 horizontalVelocity;
    private Vector2 moveInput;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private bool isMoving;
    private bool isSprinting;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

    public bool IsMoving => isMoving;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public float CurrentHorizontalSpeed => horizontalVelocity.magnitude;

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

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        ReadInput();
        CheckGround();
        QueueJump();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        MoveCharacter();
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
            groundNormal = Vector3.up;
            isGrounded = false;
            return;
        }

        bool sphereGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = controller.isGrounded || sphereGrounded;
        groundNormal = FindGroundNormal();

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedStickForce;
        }
    }

    private Vector3 FindGroundNormal()
    {
        Vector3 origin = transform.TransformPoint(controller.center);
        float rayDistance = controller.height * 0.5f + groundDistance + groundNormalProbeDistance;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.normal;
        }

        return Vector3.up;
    }

    private void ReadInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        isSprinting = Input.GetKey(KeyCode.LeftShift);
    }

    private void HandleMovement()
    {
        Vector3 desiredDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (desiredDirection.sqrMagnitude > 1f)
        {
            desiredDirection.Normalize();
        }

        if (isGrounded)
        {
            desiredDirection = Vector3.ProjectOnPlane(desiredDirection, groundNormal).normalized;
        }

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
        Vector3 desiredVelocity = desiredDirection * currentSpeed;
        bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;
        float acceleration = GetAcceleration(hasMoveInput);

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            hasMoveInput ? desiredVelocity : Vector3.zero,
            acceleration * Time.deltaTime
        );
    }

    private float GetAcceleration(bool hasMoveInput)
    {
        if (isGrounded)
        {
            return hasMoveInput ? groundAcceleration : groundDeceleration;
        }

        return hasMoveInput ? airAcceleration : airDeceleration;
    }

    private void QueueJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            lastJumpPressedTime = Time.time;
        }
    }

    private void HandleJump()
    {
        bool hasBufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canUseCoyoteTime = Time.time - lastGroundedTime <= coyoteTime;

        if (hasBufferedJump && canUseCoyoteTime)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            isGrounded = false;
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;

        if (maxFallSpeed < 0f)
        {
            velocity.y = Mathf.Max(velocity.y, maxFallSpeed);
        }
    }

    private void MoveCharacter()
    {
        Vector3 motion = horizontalVelocity + Vector3.up * velocity.y;
        CollisionFlags flags = controller.Move(motion * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
        {
            velocity.y = 0f;
        }
    }

    private void CheckMovementState()
    {
        Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        isMoving = isGrounded && flatVelocity.sqrMagnitude > 0.01f;
    }
}
