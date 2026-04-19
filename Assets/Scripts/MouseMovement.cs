using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 鼠标视角控制器
/// 负责处理第一人称视角的鼠标旋转
/// </summary>
public class MouseMove : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 100f;   // 鼠标灵敏度
    [SerializeField] private float minVerticalAngle = -90f;   // 最小垂直角度（向下看）
    [SerializeField] private float maxVerticalAngle = 90f;    // 最大垂直角度（向上看）
    private float xRotation = 0f;  // 垂直旋转角度（俯仰）
    private float yRotation = 0f;  // 水平旋转角度（偏航）

    /// <summary>
    /// 初始化：锁定并隐藏鼠标光标
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// 每帧更新：根据鼠标移动旋转视角
    /// </summary>
    private void Update()
    {
        // 获取鼠标移动量并应用灵敏度
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 更新垂直旋转（俯仰）
        // 注意：减去mouseY是因为鼠标向上移动时Y值为正，但我们希望视角向上看
        xRotation -= mouseY;
        // 限制垂直旋转角度，防止视角翻转
        xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

        // 更新水平旋转（偏航）
        yRotation += mouseX;

        // 应用旋转到摄像机
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }
}
