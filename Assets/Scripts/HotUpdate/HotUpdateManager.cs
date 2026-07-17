using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Network;
using FPSGame.Utils;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// 热更新管理器
    /// </summary>
    public class HotUpdateManager : MonoBehaviour
    {
        public static HotUpdateManager Instance { get; private set; }

        public bool HasUpdate { get; private set; }
        public bool LastDownloadSucceeded { get; private set; }
        public VersionInfo RemoteVersion { get; private set; }
        public VersionInfo LocalVersion { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        public IEnumerator CheckForUpdates()
        {
            Utils.Logger.Log("开始检查版本更新");

            // 读取本地版本
            LocalVersion = new VersionInfo
            {
                version = ConfigManager.Instance.AppVersion.version,
                buildNumber = ConfigManager.Instance.AppVersion.buildNumber
            };

            // 请求服务器版本
            string url = $"{ConfigManager.Instance.ServerConfig.serverUrl}/api/version/check";
            url += $"?platform={Application.platform}&currentVersion={LocalVersion.version}";

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            StartCoroutine(NetworkManager.Instance.Get(url, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            }));

            // 等待请求完成
            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                var response = JsonHelper.FromJson<VersionResponse>(responseData);
                if (response != null && response.success)
                {
                    RemoteVersion = response.data;
                    HasUpdate = CompareVersion(LocalVersion, RemoteVersion);

                    if (HasUpdate)
                    {
                        Utils.Logger.Log($"发现新版本: {RemoteVersion.version} (当前: {LocalVersion.version})");
                    }
                    else
                    {
                        Utils.Logger.Log("已是最新版本");
                    }
                }
                else
                {
                    Utils.Logger.LogWarning("版本检查响应格式错误");
                    HasUpdate = false;
                }
            }
            else
            {
                Utils.Logger.LogWarning($"版本检查失败，跳过更新: {responseData}");
                HasUpdate = false;
            }
        }

        /// <summary>
        /// 下载更新
        /// </summary>
        public IEnumerator DownloadUpdates(Action<float> onProgress = null)
        {
            LastDownloadSucceeded = false;

            if (!HasUpdate || RemoteVersion == null)
            {
                Utils.Logger.LogWarning("No updates available");
                yield break;
            }

            Utils.Logger.Log($"Starting download, size: {RemoteVersion.totalSize / 1024 / 1024}MB");

            // 下载资源清单
            string manifestUrl = AddCacheBuster(RemoteVersion.catalogUrl, RemoteVersion.buildNumber.ToString());
            bool manifestDownloaded = false;
            AssetManifest remoteManifest = null;

            yield return StartCoroutine(DownloadManifest(manifestUrl, (success, manifest) =>
            {
                manifestDownloaded = success;
                remoteManifest = manifest;
            }));

            if (!manifestDownloaded || remoteManifest == null)
            {
                Utils.Logger.LogError("Failed to download manifest");
                yield break;
            }

            // 计算需要下载的文件
            List<AssetBundleInfo> bundlesToDownload = new List<AssetBundleInfo>();
            string cachePath = AssetBundleManager.CachePath;

            foreach (var bundleInfo in remoteManifest.bundles)
            {
                string localPath = System.IO.Path.Combine(cachePath, bundleInfo.name);

                // 检查本地是否已有且哈希匹配
                if (System.IO.File.Exists(localPath))
                {
                    string localHash = DownloadManager.CalculateMD5(localPath);
                    if (localHash == bundleInfo.hash)
                    {
                        continue; // 跳过已存在且正确的文件
                    }
                }

                bundlesToDownload.Add(bundleInfo);
            }

            if (bundlesToDownload.Count == 0)
            {
                Utils.Logger.Log("All files are up to date");
                ConfigManager.Instance.SaveAppVersion(RemoteVersion.version, RemoteVersion.buildNumber);
                LastDownloadSucceeded = true;
                yield break;
            }

            Utils.Logger.Log($"Need to download {bundlesToDownload.Count} files");

            // 下载文件
            int totalFiles = bundlesToDownload.Count;
            int downloadedFiles = 0;
            bool downloadFailed = false;

            foreach (var bundleInfo in bundlesToDownload)
            {
                string bundleUrl = GetBundleUrl(bundleInfo.name, bundleInfo.hash);
                string savePath = System.IO.Path.Combine(cachePath, bundleInfo.name);

                bool fileDownloaded = false;
                bool fileSuccess = false;

                DownloadManager.Instance.AddDownload(
                    bundleUrl,
                    savePath,
                    bundleInfo.hash,
                    (success, message) =>
                    {
                        fileDownloaded = true;
                        fileSuccess = success;
                        if (!success)
                        {
                            Utils.Logger.LogError($"Download failed: {bundleInfo.name} - {message}");
                            downloadFailed = true;
                        }
                    },
                    (progress) =>
                    {
                        float totalProgress = (downloadedFiles + progress) / totalFiles;
                        onProgress?.Invoke(totalProgress);
                    }
                );

                // 等待当前文件下载完成
                yield return new WaitUntil(() => fileDownloaded);

                if (!fileSuccess)
                {
                    break;
                }

                downloadedFiles++;
                onProgress?.Invoke((float)downloadedFiles / totalFiles);
            }

            if (downloadFailed)
            {
                Utils.Logger.LogError("Update download failed");
                yield break;
            }

            // 更新本地版本号
            ConfigManager.Instance.SaveAppVersion(RemoteVersion.version, RemoteVersion.buildNumber);
            LastDownloadSucceeded = true;
            Utils.Logger.Log("Update completed successfully");
        }

        /// <summary>
        /// 下载资源清单
        /// </summary>
        private IEnumerator DownloadManifest(string url, Action<bool, AssetManifest> onComplete)
        {
            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            StartCoroutine(NetworkManager.Instance.Get(url, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            }));

            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                try
                {
                    AssetManifest manifest = JsonUtility.FromJson<AssetManifest>(responseData);
                    onComplete?.Invoke(true, manifest);
                }
                catch (Exception e)
                {
                    Utils.Logger.LogError($"Failed to parse manifest: {e.Message}");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Utils.Logger.LogError($"Failed to download manifest: {responseData}");
                onComplete?.Invoke(false, null);
            }
        }

        /// <summary>
        /// 获取 Bundle 下载地址
        /// </summary>
        private string GetBundleUrl(string bundleName, string hash)
        {
            string baseUrl = ConfigManager.Instance.ServerConfig.serverUrl;
            return AddCacheBuster($"{baseUrl}/updates/bundles/{bundleName}", hash);
        }

        private static string AddCacheBuster(string url, string value)
        {
            string separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}v={value}";
        }

        /// <summary>
        /// 比较版本号
        /// </summary>
        private bool CompareVersion(VersionInfo local, VersionInfo remote)
        {
            if (remote == null) return false;
            return remote.buildNumber > local.buildNumber;
        }
    }

    /// <summary>
    /// 版本信息
    /// </summary>
    [Serializable]
    public class VersionInfo
    {
        public string version;
        public int buildNumber;
        public string catalogUrl;
        public long totalSize;
        public bool forceUpdate;
        public string updateDescription;
    }

    /// <summary>
    /// 版本检查响应
    /// </summary>
    [Serializable]
    public class VersionResponse
    {
        public bool success;
        public VersionInfo data;
    }
}
