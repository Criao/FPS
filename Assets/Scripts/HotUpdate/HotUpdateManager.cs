using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Network;
using FPSGame.Utils;

namespace FPSGame.HotUpdate
{
    public class HotUpdateManager : MonoBehaviour
    {
        public static HotUpdateManager Instance { get; private set; }

        public bool HasUpdate { get; private set; }
        public bool LastCheckSucceeded { get; private set; }
        public bool LastDownloadSucceeded { get; private set; }
        public string LastErrorMessage { get; private set; }
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

        public IEnumerator CheckForUpdates()
        {
            HasUpdate = false;
            LastCheckSucceeded = false;
            LastErrorMessage = string.Empty;
            RemoteVersion = null;
            Utils.Logger.Log("Checking app version");

            LocalVersion = new VersionInfo
            {
                version = ConfigManager.Instance.AppVersion.version,
                buildNumber = ConfigManager.Instance.AppVersion.buildNumber
            };

            string url = $"{ConfigManager.Instance.ServerConfig.serverUrl}/api/version/check";
            url += $"?platform={Application.platform}&currentVersion={LocalVersion.version}";

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = string.Empty;

            StartCoroutine(NetworkManager.Instance.Get(url, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            }));

            yield return new WaitUntil(() => requestComplete);

            if (!requestSuccess)
            {
                LastErrorMessage = $"Version check failed: {responseData}";
                Utils.Logger.LogWarning(LastErrorMessage);
                HasUpdate = false;
                yield break;
            }

            VersionResponse response = JsonHelper.FromJson<VersionResponse>(responseData);
            if (response == null || !response.success || response.data == null)
            {
                LastErrorMessage = "Version check response is invalid";
                Utils.Logger.LogWarning(LastErrorMessage);
                HasUpdate = false;
                yield break;
            }

            RemoteVersion = response.data;
            HasUpdate = CompareVersion(LocalVersion, RemoteVersion);
            LastCheckSucceeded = true;

            if (HasUpdate)
            {
                Utils.Logger.Log($"New version found: {RemoteVersion.version} (current: {LocalVersion.version})");
            }
            else
            {
                Utils.Logger.Log("Already on latest version");
            }
        }

        public IEnumerator DownloadUpdates(Action<float> onProgress = null)
        {
            LastDownloadSucceeded = false;
            LastErrorMessage = string.Empty;

            if (!HasUpdate || RemoteVersion == null)
            {
                LastErrorMessage = "No updates available";
                Utils.Logger.LogWarning(LastErrorMessage);
                yield break;
            }

            Utils.Logger.Log($"Starting download, size: {RemoteVersion.totalSize / 1024 / 1024}MB");

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
                LastErrorMessage = "Failed to download manifest";
                Utils.Logger.LogError(LastErrorMessage);
                yield break;
            }

            List<AssetBundleInfo> bundlesToDownload = GetBundlesToDownload(remoteManifest);
            if (bundlesToDownload.Count == 0)
            {
                Utils.Logger.Log("All files are up to date");
                MarkUpdateInstalled(remoteManifest);
                onProgress?.Invoke(1f);
                yield break;
            }

            Utils.Logger.Log($"Need to download {bundlesToDownload.Count} files");

            int totalFiles = bundlesToDownload.Count;
            int downloadedFiles = 0;
            bool downloadFailed = false;

            foreach (AssetBundleInfo bundleInfo in bundlesToDownload)
            {
                string bundleUrl = GetBundleUrl(bundleInfo.name, bundleInfo.hash);
                string savePath = System.IO.Path.Combine(AssetBundleManager.CachePath, bundleInfo.name);

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
                            LastErrorMessage = $"Download failed: {bundleInfo.name} - {message}";
                            Utils.Logger.LogError(LastErrorMessage);
                            downloadFailed = true;
                        }
                    },
                    progress =>
                    {
                        float totalProgress = (downloadedFiles + progress) / totalFiles;
                        onProgress?.Invoke(totalProgress);
                    }
                );

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
                if (string.IsNullOrEmpty(LastErrorMessage))
                {
                    LastErrorMessage = "Update download failed";
                }

                Utils.Logger.LogError(LastErrorMessage);
                yield break;
            }

            MarkUpdateInstalled(remoteManifest);
            onProgress?.Invoke(1f);
            Utils.Logger.Log("Update completed successfully");
        }

        private List<AssetBundleInfo> GetBundlesToDownload(AssetManifest remoteManifest)
        {
            List<AssetBundleInfo> bundlesToDownload = new List<AssetBundleInfo>();
            string cachePath = AssetBundleManager.CachePath;

            foreach (AssetBundleInfo bundleInfo in remoteManifest.bundles)
            {
                string localPath = System.IO.Path.Combine(cachePath, bundleInfo.name);
                if (System.IO.File.Exists(localPath))
                {
                    string localHash = DownloadManager.CalculateMD5(localPath);
                    if (string.Equals(localHash, bundleInfo.hash, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                bundlesToDownload.Add(bundleInfo);
            }

            return bundlesToDownload;
        }

        private void MarkUpdateInstalled(AssetManifest manifest)
        {
            string installedVersion = !string.IsNullOrEmpty(manifest.version)
                ? manifest.version
                : RemoteVersion.version;

            int installedBuildNumber = manifest.buildNumber > 0
                ? manifest.buildNumber
                : RemoteVersion.buildNumber;

            ConfigManager.Instance.SaveAppVersion(installedVersion, installedBuildNumber);

            LocalVersion = new VersionInfo
            {
                version = installedVersion,
                buildNumber = installedBuildNumber,
                catalogUrl = RemoteVersion.catalogUrl,
                totalSize = RemoteVersion.totalSize,
                forceUpdate = RemoteVersion.forceUpdate,
                updateDescription = RemoteVersion.updateDescription
            };

            HasUpdate = false;
            LastDownloadSucceeded = true;
            LastErrorMessage = string.Empty;
        }

        private IEnumerator DownloadManifest(string url, Action<bool, AssetManifest> onComplete)
        {
            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = string.Empty;

            StartCoroutine(NetworkManager.Instance.Get(url, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            }));

            yield return new WaitUntil(() => requestComplete);

            if (!requestSuccess)
            {
                LastErrorMessage = $"Failed to download manifest: {responseData}";
                Utils.Logger.LogError(LastErrorMessage);
                onComplete?.Invoke(false, null);
                yield break;
            }

            try
            {
                AssetManifest manifest = JsonUtility.FromJson<AssetManifest>(responseData);
                onComplete?.Invoke(true, manifest);
            }
            catch (Exception e)
            {
                LastErrorMessage = $"Failed to parse manifest: {e.Message}";
                Utils.Logger.LogError(LastErrorMessage);
                onComplete?.Invoke(false, null);
            }
        }

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

        private bool CompareVersion(VersionInfo local, VersionInfo remote)
        {
            return remote != null && remote.buildNumber > local.buildNumber;
        }
    }

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

    [Serializable]
    public class VersionResponse
    {
        public bool success;
        public VersionInfo data;
    }
}
