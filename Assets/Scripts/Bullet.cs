using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹脚本
/// 负责处理子弹的碰撞和伤害
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("子弹设置")]
    [SerializeField] private float damage = 25f;                   // 伤害值

    [Header("弹孔效果")]
    [SerializeField] private GameObject bulletHolePrefab;          // 弹孔预制件
    [SerializeField] private float bulletHoleSize = 0.1f;          // 弹孔大小
    [SerializeField] private float bulletHoleLifetime = 10f;       // 弹孔存在时间

    private void OnCollisionEnter(Collision collision)
    {
        // 生成弹孔效果
        CreateBulletHole(collision);

        if (collision.gameObject.CompareTag("Target"))
        {
            print("hit " + collision.gameObject.name + " !");
            Destroy(gameObject);
        }
        else if (collision.gameObject.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 创建弹孔效果
    /// </summary>
    private void CreateBulletHole(Collision collision)
    {
        if (bulletHolePrefab == null) return;

        // 获取碰撞点信息
        ContactPoint contact = collision.contacts[0];
        Vector3 hitPosition = contact.point;
        Vector3 hitNormal = contact.normal;

        // 创建弹孔，使其朝向表面法线方向
        Quaternion hitRotation = Quaternion.LookRotation(hitNormal);
        GameObject bulletHole = Instantiate(bulletHolePrefab, hitPosition, hitRotation);

        // 稍微偏移弹孔，避免Z-fighting
        bulletHole.transform.position += hitNormal * 0.001f;

        // 设置弹孔大小（添加随机变化）
        float randomSize = bulletHoleSize * Random.Range(0.8f, 1.2f);
        bulletHole.transform.localScale = Vector3.one * randomSize;

        // 随机旋转弹孔（绕法线旋转）
        bulletHole.transform.Rotate(hitNormal, Random.Range(0f, 360f), Space.World);

        // 将弹孔设为击中物体的子物体（可选）
        bulletHole.transform.SetParent(collision.transform);

        // 在指定时间后销毁弹孔
        Destroy(bulletHole, bulletHoleLifetime);
    }
}
