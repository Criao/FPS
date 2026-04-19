using System.Collections;
using UnityEngine;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// 热更新资源加载器示例
    /// </summary>
    public class HotUpdateLoader : MonoBehaviour
    {
        void Start()
        {
            // 确保管理器存在
            if (FindObjectOfType<FPSGame.Core.ConfigManager>() == null)
            {
                GameObject go = new GameObject("ConfigManager");
                go.AddComponent<FPSGame.Core.ConfigManager>();
            }

            if (FindObjectOfType<FPSGame.Network.NetworkManager>() == null)
            {
                GameObject go = new GameObject("NetworkManager");
                go.AddComponent<FPSGame.Network.NetworkManager>();
            }

            if (FindObjectOfType<HotUpdateManager>() == null)
            {
                GameObject go = new GameObject("HotUpdateManager");
                go.AddComponent<HotUpdateManager>();
            }

            if (FindObjectOfType<DownloadManager>() == null)
            {
                GameObject go = new GameObject("DownloadManager");
                go.AddComponent<DownloadManager>();
            }

            StartCoroutine(LoadHotUpdateContent());
        }

        IEnumerator LoadHotUpdateContent()
        {
            // 1. 检查更新
            yield return HotUpdateManager.Instance.CheckForUpdates();

            if (HotUpdateManager.Instance.HasUpdate)
            {
                Debug.Log("发现新版本，开始下载...");

                // 2. 下载更新
                yield return HotUpdateManager.Instance.DownloadUpdates(
                    progress => Debug.Log($"下载进度: {progress * 100:F1}%")
                );

                Debug.Log("更新下载完成");
            }

            // 3. 加载 AssetBundle 中的方块
            yield return AssetBundleManager.Instance.Initialize();
            yield return LoadCube();
        }

        IEnumerator LoadCube()
        {
            // 从 AssetBundle 加载 Prefab
            GameObject cubePrefab = null;
            yield return AssetBundleManager.Instance.LoadAsset<GameObject>(
                "cubes.unity3d",  // Bundle 名称
                "HotUpdateCube",  // Prefab 名称
                (loadedPrefab) => cubePrefab = loadedPrefab
            );

            if (cubePrefab != null)
            {
                // 实例化方块
                GameObject cube = Instantiate(cubePrefab, new Vector3(0, 1, 5), Quaternion.identity);
                Debug.Log("热更新方块加载成功！");
            }
            else
            {
                Debug.LogError("加载方块失败");
            }
        }
    }
}
