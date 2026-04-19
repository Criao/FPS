using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家移动控制器
/// 负责处理玩家的移动、冲刺、跳跃和重力
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;        // 正常移动速度
    [SerializeField] private float sprintSpeed = 8f;      // 冲刺速度
    [SerializeField] private float jumpForce = 5f;        // 跳跃力度
    [SerializeField] private float gravity = -9.81f;      // 重力加速度

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;       // 地面检测点位置
    [SerializeField] private float groundDistance = 0.4f; // 地面检测距离
    [SerializeField] private LayerMask groundMask;        // 地面图层遮罩

    private CharacterController controller;  // 角色控制器组件
    private Vector3 velocity;                // 当前速度向量
    private bool isGrounded;                 // 是否在地面上
    private bool isMoving;                   // 是否正在移动
    private Vector3 lastPosition;            // 上一帧的位置

    /// <summary>
    /// 初始化：获取角色控制器组件并记录初始位置
    /// </summary>
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    /// <summary>
    /// 每帧更新：按顺序处理地面检测、移动、跳跃和重力
    /// </summary>
    private void Update()
    {
        CheckGround();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        CheckMovementState();
    }

    /// <summary>
    /// 检测玩家是否在地面上
    /// 使用球形检测判断地面接触
    /// </summary>
    private void CheckGround()
    {
        // 在地面检测点位置创建一个球形检测区域
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // 如果在地面上且垂直速度为负，重置垂直速度为一个小的负值
        // 这样可以保持角色贴在地面上
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    /// <summary>
    /// 处理玩家的水平移动
    /// 支持WASD移动和左Shift冲刺
    /// </summary>
    private void HandleMovement()
    {
        // 获取输入轴（WASD或方向键）
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 计算移动方向（相对于玩家朝向）
        Vector3 move = transform.right * horizontal + transform.forward * vertical;

        // 根据是否按下左Shift键选择移动速度
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 处理玩家跳跃
    /// 只有在地面上时才能跳跃
    /// </summary>
    private void HandleJump()
    {
        // 调试：检测跳跃按键和地面状态
        if (Input.GetButtonDown("Jump"))
        {
            Debug.Log($"按下跳跃键 - 是否在地面: {isGrounded}");
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // 使用物理公式计算跳跃所需的初速度
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            Debug.Log("执行跳跃！");
        }
    }

    /// <summary>
    /// 应用重力效果
    /// 每帧更新垂直速度并移动角色
    /// </summary>
    private void ApplyGravity()
    {
        // 累加重力加速度
        velocity.y += gravity * Time.deltaTime;
        // 应用垂直移动
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// 检测玩家移动状态
    /// 通过比较当前位置和上一帧位置判断是否在移动
    /// </summary>
    private void CheckMovementState()
    {
        // 如果位置发生变化且在地面上，则判定为正在移动
        if (lastPosition != transform.position && isGrounded)
        {
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }

        // 更新上一帧位置
        lastPosition = transform.position;
    }
}
