using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// AssetBundle 管理器 - 负责加载和缓存 AssetBundle
    /// </summary>
    public class AssetBundleManager : MonoBehaviour
    {
        private static AssetBundleManager _instance;
        public static AssetBundleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("AssetBundleManager");
                    _instance = go.AddComponent<AssetBundleManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // 缓存已加载的 AssetBundle
        private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        // AssetBundle 依赖关系
        private AssetBundleManifest manifest;

        // 本地缓存路径
        public static string CachePath
        {
            get { return Path.Combine(Application.persistentDataPath, "AssetBundles"); }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// 初始化 - 加载主清单文件
        /// </summary>
        public IEnumerator Initialize()
        {
            string manifestPath = Path.Combine(CachePath, "AssetBundles");

            if (File.Exists(manifestPath))
            {
                AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(manifestPath);
                yield return request;

                if (request.assetBundle != null)
                {
                    manifest = request.assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    request.assetBundle.Unload(false);
                    Utils.Logger.Log("AssetBundle manifest loaded");
                }
            }
            else
            {
                Utils.Logger.LogWarning("AssetBundle manifest not found");
            }
        }

        /// <summary>
        /// 加载 AssetBundle
        /// </summary>
        public IEnumerator LoadAssetBundle(string bundleName, Action<AssetBundle> onComplete)
        {
            // 如果已经加载，直接返回
            if (loadedBundles.ContainsKey(bundleName))
            {
                onComplete?.Invoke(loadedBundles[bundleName]);
                yield break;
            }

            // 先加载依赖
            if (manifest != null)
            {
                string[] dependencies = manifest.GetAllDependencies(bundleName);
                foreach (string dependency in dependencies)
                {
                    if (!loadedBundles.ContainsKey(dependency))
                    {
                        yield return StartCoroutine(LoadAssetBundleInternal(dependency));
                    }
                }
            }

            // 加载主 Bundle
            yield return StartCoroutine(LoadAssetBundleInternal(bundleName));

            onComplete?.Invoke(loadedBundles.ContainsKey(bundleName) ? loadedBundles[bundleName] : null);
        }

        private IEnumerator LoadAssetBundleInternal(string bundleName)
        {
            string bundlePath = Path.Combine(CachePath, bundleName);

            if (!File.Exists(bundlePath))
            {
                Utils.Logger.LogError($"AssetBundle not found: {bundleName}");
                yield break;
            }

            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
            yield return request;

            if (request.assetBundle != null)
            {
                loadedBundles[bundleName] = request.assetBundle;
                Utils.Logger.Log($"AssetBundle loaded: {bundleName}");
            }
            else
            {
                Utils.Logger.LogError($"Failed to load AssetBundle: {bundleName}");
            }
        }

        /// <summary>
        /// 从 AssetBundle 加载资源
        /// </summary>
        public IEnumerator LoadAsset<T>(string bundleName, string assetName, Action<T> onComplete) where T : UnityEngine.Object
        {
            AssetBundle bundle = null;
            yield return StartCoroutine(LoadAssetBundle(bundleName, (loadedBundle) => bundle = loadedBundle));

            if (bundle != null)
            {
                AssetBundleRequest request = bundle.LoadAssetAsync<T>(assetName);
                yield return request;

                onComplete?.Invoke(request.asset as T);
            }
            else
            {
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// 卸载 AssetBundle
        /// </summary>
        public void UnloadAssetBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            if (loadedBundles.ContainsKey(bundleName))
            {
                loadedBundles[bundleName].Unload(unloadAllLoadedObjects);
                loadedBundles.Remove(bundleName);
                Utils.Logger.Log($"AssetBundle unloaded: {bundleName}");
            }
        }

        /// <summary>
        /// 卸载所有 AssetBundle
        /// </summary>
        public void UnloadAllAssetBundles(bool unloadAllLoadedObjects = false)
        {
            foreach (var bundle in loadedBundles.Values)
            {
                bundle.Unload(unloadAllLoadedObjects);
            }
            loadedBundles.Clear();
            Utils.Logger.Log("All AssetBundles unloaded");
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            UnloadAllAssetBundles(true);

            if (Directory.Exists(CachePath))
            {
                Directory.Delete(CachePath, true);
                Utils.Logger.Log("AssetBundle cache cleared");
            }
        }

        /// <summary>
        /// 获取缓存大小
        /// </summary>
        public long GetCacheSize()
        {
            if (!Directory.Exists(CachePath))
                return 0;

            long size = 0;
            DirectoryInfo dirInfo = new DirectoryInfo(CachePath);
            foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
            return size;
        }

        private void OnDestroy()
        {
            UnloadAllAssetBundles(false);
        }
    }
}
