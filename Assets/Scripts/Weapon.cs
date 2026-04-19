using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 射击模式枚举
/// </summary>
public enum ShootingMode
{
    Single,     // 单发
    Burst,      // 连发
    Auto        // 全自动
}

/// <summary>
/// 武器系统
/// 负责处理射击功能
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("引用")]
    public Camera playerCamera;                                    // 玩家摄像机

    [Header("射击模式")]
    public ShootingMode currentShootingMode;                       // 当前射击模式

    [Header("子弹设置")]
    [SerializeField] private GameObject bulletPrefab;              // 子弹预制件
    [SerializeField] private Transform bulletSpawnPoint;           // 子弹生成位置
    [SerializeField] private float bulletSpeed = 30f;              // 子弹飞行速度
    [SerializeField] private float bulletLifetime = 3f;            // 子弹生命周期（秒）

    [Header("射击设置")]
    public bool isShooting, readyToShoot;                          // 射击状态
    bool allowReset = true;                                        // 允许重置
    public float shootingDelay = 0.1f;                             // 连发间隔（秒）
    public float fireRate = 0.5f;                                  // 射击冷却时间（秒）

    [Header("连发设置")]
    public int bulletsPerBurst = 3;                                // 每次连发子弹数
    public int currentBurst;                                       // 当前连发计数

    [Header("扩散设置")]
    public float spreadIntensity;                                  // 扩散强度

    private void Start()
    {
        readyToShoot = true;
        currentBurst = bulletsPerBurst;
    }

    private void Update()
    {
        HandleShooting();
        HandleModeSwitch();
    }

    /// <summary>
    /// 处理射击模式切换
    /// </summary>
    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            // 切换到下一个射击模式
            currentShootingMode = (ShootingMode)(((int)currentShootingMode + 1) % 3);
            Debug.Log("切换射击模式: " + currentShootingMode);
        }
    }

    /// <summary>
    /// 处理射击逻辑
    /// </summary>
    private void HandleShooting()
    {
        // 检查是否做好射击准备
        if (!readyToShoot) return;

        // 根据不同射击模式处理输入
        switch (currentShootingMode)
        {
            case ShootingMode.Single:
                // 单发模式：每次点击射击一次
                if (Input.GetButtonDown("Fire1"))
                {
                    currentBurst = 1;
                    Shoot();
                }
                break;

            case ShootingMode.Burst:
                // 连发模式：每次点击射击多发
                if (Input.GetButtonDown("Fire1"))
                {
                    currentBurst = bulletsPerBurst;
                    Shoot();
                }
                break;

            case ShootingMode.Auto:
                // 全自动模式：按住持续射击
                if (Input.GetButton("Fire1"))
                {
                    currentBurst = 1;
                    Shoot();
                }
                break;
        }
    }

    /// <summary>
    /// 射击
    /// </summary>
    private void Shoot()
    {
        // 射击开始，设置为false防止连续射击
        readyToShoot = false;

        // 计算射击方向（包含散布）
        Vector3 shootingDirection = CalculateDirectionAndSpread();

        // 生成子弹
        if (bulletPrefab != null && bulletSpawnPoint != null)
        {
            // 子弹实例化，将子弹前向轴对准射击方向
            GameObject bullet = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.LookRotation(shootingDirection));

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = shootingDirection * bulletSpeed;
            }

            StartCoroutine(DestroyBulletAfterTime(bullet, bulletLifetime));
        }

        currentBurst--;

        // 实现射击重置
        if (currentBurst > 0)
        {
            // 还有子弹要射击，使用连发间隔继续射击
            Invoke("Shoot", shootingDelay);
        }
        else
        {
            // 连发完成，使用射击冷却时间后重置
            if (allowReset)
            {
                Invoke("ResetShot", fireRate);
                allowReset = false;
            }
        }
    }

    /// <summary>
    /// 重置射击状态
    /// </summary>
    private void ResetShot()
    {
        readyToShoot = true;
        allowReset = true;  // 重置为真，允许下次射击后重置
    }

    /// <summary>
    /// 计算射击方向和散布
    /// </summary>
    private Vector3 CalculateDirectionAndSpread()
    {
        // 从屏幕中心发射射线检测瞄准点
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out hit))
        {
            // 射线击中物体，使用击中点作为目标
            targetPoint = hit.point;
        }
        else
        {
            // 射线未击中，使用射线前方100单位处作为目标
            targetPoint = ray.GetPoint(100f);
        }

        // 计算从子弹生成点到目标点的方向
        Vector3 direction = targetPoint - bulletSpawnPoint.position;

        // 添加扩散
        if (spreadIntensity > 0)
        {
            direction += new Vector3(
                Random.Range(-spreadIntensity, spreadIntensity),
                Random.Range(-spreadIntensity, spreadIntensity),
                0
            );
        }

        // 归一化处理
        direction.Normalize();

        return direction;
    }
    private IEnumerator DestroyBulletAfterTime(GameObject bullet, float delay)
    {
        // 等待指定时间
        yield return new WaitForSeconds(delay);

        // 销毁子弹（如果子弹还存在）
        if (bullet != null)
        {
            Destroy(bullet);
        }
    }
}
