using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FPSGame.HotUpdate
{
    public class AssetBundleManager : MonoBehaviour
    {
        private static AssetBundleManager instance;

        private readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
        private AssetBundleManifest manifest;

        public static AssetBundleManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AssetBundleManager");
                    instance = go.AddComponent<AssetBundleManager>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        public static string CachePath => Path.Combine(Application.persistentDataPath, "AssetBundles");

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator Initialize()
        {
            string manifestPath = ResolveBundlePath("bundles");
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                manifestPath = ResolveBundlePath("AssetBundles");
            }

            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                Utils.Logger.LogWarning("AssetBundle manifest not found. Direct bundle loading will still be attempted.");
                yield break;
            }

            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(manifestPath);
            yield return request;

            if (request.assetBundle == null)
            {
                Utils.Logger.LogWarning($"Failed to load AssetBundle manifest: {manifestPath}");
                yield break;
            }

            manifest = request.assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            request.assetBundle.Unload(false);

            if (manifest != null)
            {
                Utils.Logger.Log("AssetBundle manifest loaded");
            }
            else
            {
                Utils.Logger.LogWarning($"AssetBundleManifest asset missing in: {manifestPath}");
            }
        }

        public IEnumerator LoadAssetBundle(string bundleName, Action<AssetBundle> onComplete)
        {
            if (loadedBundles.TryGetValue(bundleName, out AssetBundle cachedBundle))
            {
                onComplete?.Invoke(cachedBundle);
                yield break;
            }

            string manifestBundleName = GetManifestBundleName(bundleName);
            if (manifest != null && !string.IsNullOrEmpty(manifestBundleName))
            {
                string[] dependencies = manifest.GetAllDependencies(manifestBundleName);
                foreach (string dependency in dependencies)
                {
                    if (!loadedBundles.ContainsKey(dependency))
                    {
                        yield return StartCoroutine(LoadAssetBundleInternal(dependency));
                    }
                }
            }

            yield return StartCoroutine(LoadAssetBundleInternal(bundleName));
            onComplete?.Invoke(loadedBundles.TryGetValue(bundleName, out AssetBundle loadedBundle) ? loadedBundle : null);
        }

        public IEnumerator LoadAsset<T>(string bundleName, string assetName, Action<T> onComplete)
            where T : UnityEngine.Object
        {
            AssetBundle bundle = null;
            yield return StartCoroutine(LoadAssetBundle(bundleName, loadedBundle => bundle = loadedBundle));

            if (bundle == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            AssetBundleRequest request = bundle.LoadAssetAsync<T>(assetName);
            yield return request;

            onComplete?.Invoke(request.asset as T);
        }

        public void UnloadAssetBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            if (!loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                return;
            }

            bundle.Unload(unloadAllLoadedObjects);
            loadedBundles.Remove(bundleName);
            Utils.Logger.Log($"AssetBundle unloaded: {bundleName}");
        }

        public void UnloadAllAssetBundles(bool unloadAllLoadedObjects = false)
        {
            foreach (AssetBundle bundle in loadedBundles.Values)
            {
                bundle.Unload(unloadAllLoadedObjects);
            }

            loadedBundles.Clear();
            Utils.Logger.Log("All AssetBundles unloaded");
        }

        public void ClearCache()
        {
            UnloadAllAssetBundles(true);

            if (!Directory.Exists(CachePath))
            {
                return;
            }

            Directory.Delete(CachePath, true);
            Utils.Logger.Log("AssetBundle cache cleared");
        }

        public long GetCacheSize()
        {
            if (!Directory.Exists(CachePath))
            {
                return 0;
            }

            long size = 0;
            DirectoryInfo directory = new DirectoryInfo(CachePath);
            foreach (FileInfo file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }

            return size;
        }

        private string GetManifestBundleName(string bundleName)
        {
            if (manifest == null || string.IsNullOrEmpty(bundleName))
            {
                return null;
            }

            string normalizedBundleName = bundleName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                ? bundleName.Substring(0, bundleName.Length - ".unity3d".Length)
                : bundleName;

            foreach (string manifestBundleName in manifest.GetAllAssetBundles())
            {
                if (string.Equals(manifestBundleName, bundleName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(manifestBundleName, normalizedBundleName, StringComparison.OrdinalIgnoreCase))
                {
                    return manifestBundleName;
                }
            }

            return null;
        }

        private IEnumerator LoadAssetBundleInternal(string bundleName)
        {
            string bundlePath = ResolveBundlePath(bundleName);
            if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath))
            {
                Utils.Logger.LogError($"AssetBundle not found: {bundleName}");
                yield break;
            }

            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
            yield return request;

            if (request.assetBundle == null)
            {
                Utils.Logger.LogError($"Failed to load AssetBundle: {bundleName}");
                yield break;
            }

            loadedBundles[bundleName] = request.assetBundle;
            Utils.Logger.Log($"AssetBundle loaded: {bundleName}");
        }

        private static string ResolveBundlePath(string bundleName)
        {
            string cacheBundlePath = Path.Combine(CachePath, bundleName);
            if (File.Exists(cacheBundlePath))
            {
                return cacheBundlePath;
            }

            string cacheUnity3dPath = GetUnity3dVariantPath(cacheBundlePath);
            if (File.Exists(cacheUnity3dPath))
            {
                return cacheUnity3dPath;
            }

#if UNITY_EDITOR
            string editorServerBundlePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Server/public/updates/bundles",
                bundleName));

            if (File.Exists(editorServerBundlePath))
            {
                Utils.Logger.LogWarning($"Using editor hot-update bundle fallback: {editorServerBundlePath}");
                return editorServerBundlePath;
            }

            string editorServerUnity3dPath = GetUnity3dVariantPath(editorServerBundlePath);
            if (File.Exists(editorServerUnity3dPath))
            {
                Utils.Logger.LogWarning($"Using editor hot-update bundle fallback: {editorServerUnity3dPath}");
                return editorServerUnity3dPath;
            }
#endif

            return cacheBundlePath;
        }

        private static string GetUnity3dVariantPath(string bundlePath)
        {
            return bundlePath.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                ? bundlePath
                : bundlePath + ".unity3d";
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            UnloadAllAssetBundles(false);
            instance = null;
        }
    }
}
