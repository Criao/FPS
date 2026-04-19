using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace FPSGame.HotUpdate
{
    /// <summary>
    /// 下载管理器 - 负责下载和校验资源文件
    /// </summary>
    public class DownloadManager : MonoBehaviour
    {
        private static DownloadManager _instance;
        public static DownloadManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("DownloadManager");
                    _instance = go.AddComponent<DownloadManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Queue<DownloadTask> downloadQueue = new Queue<DownloadTask>();
        private DownloadTask currentTask;
        private bool isDownloading = false;

        public int MaxRetryCount = 3;
        public float TimeoutSeconds = 30f;

        private class DownloadTask
        {
            public string url;
            public string savePath;
            public string expectedHash;
            public int retryCount = 0;
            public Action<bool, string> onComplete;
            public Action<float> onProgress;
        }

        /// <summary>
        /// 添加下载任务到队列
        /// </summary>
        public void AddDownload(string url, string savePath, string expectedHash,
            Action<bool, string> onComplete, Action<float> onProgress = null)
        {
            DownloadTask task = new DownloadTask
            {
                url = url,
                savePath = savePath,
                expectedHash = expectedHash,
                onComplete = onComplete,
                onProgress = onProgress
            };

            downloadQueue.Enqueue(task);

            if (!isDownloading)
            {
                StartCoroutine(ProcessDownloadQueue());
            }
        }

        private IEnumerator ProcessDownloadQueue()
        {
            isDownloading = true;

            while (downloadQueue.Count > 0)
            {
                currentTask = downloadQueue.Dequeue();
                yield return StartCoroutine(DownloadFile(currentTask));
            }

            isDownloading = false;
            currentTask = null;
        }

        private IEnumerator DownloadFile(DownloadTask task)
        {
            // 确保目录存在
            string directory = Path.GetDirectoryName(task.savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 如果文件已存在且哈希匹配，跳过下载
            if (File.Exists(task.savePath))
            {
                string existingHash = CalculateMD5(task.savePath);
                if (existingHash == task.expectedHash)
                {
                    Utils.Logger.Log($"File already exists and verified: {Path.GetFileName(task.savePath)}");
                    task.onComplete?.Invoke(true, "File already exists");
                    yield break;
                }
                else
                {
                    // 哈希不匹配，删除旧文件
                    File.Delete(task.savePath);
                }
            }

            Utils.Logger.Log($"Downloading: {task.url}");

            UnityWebRequest request = UnityWebRequest.Get(task.url);
            request.downloadHandler = new DownloadHandlerFile(task.savePath);
            request.timeout = (int)TimeoutSeconds;

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                task.onProgress?.Invoke(request.downloadProgress);
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 校验文件
                string fileHash = CalculateMD5(task.savePath);
                if (fileHash == task.expectedHash)
                {
                    Utils.Logger.Log($"Download successful: {Path.GetFileName(task.savePath)}");
                    task.onComplete?.Invoke(true, "Download successful");
                }
                else
                {
                    Utils.Logger.LogError($"Hash mismatch: {Path.GetFileName(task.savePath)}");
                    File.Delete(task.savePath);

                    // 重试
                    if (task.retryCount < MaxRetryCount)
                    {
                        task.retryCount++;
                        Utils.Logger.Log($"Retrying download ({task.retryCount}/{MaxRetryCount})...");
                        downloadQueue.Enqueue(task);
                    }
                    else
                    {
                        task.onComplete?.Invoke(false, "Hash verification failed");
                    }
                }
            }
            else
            {
                Utils.Logger.LogError($"Download failed: {request.error}");

                // 重试
                if (task.retryCount < MaxRetryCount)
                {
                    task.retryCount++;
                    Utils.Logger.Log($"Retrying download ({task.retryCount}/{MaxRetryCount})...");
                    downloadQueue.Enqueue(task);
                }
                else
                {
                    task.onComplete?.Invoke(false, request.error);
                }
            }

            request.Dispose();
        }

        /// <summary>
        /// 计算文件的MD5哈希值
        /// </summary>
        public static string CalculateMD5(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }

        /// <summary>
        /// 获取当前下载进度
        /// </summary>
        public int GetQueueCount()
        {
            return downloadQueue.Count + (currentTask != null ? 1 : 0);
        }
    }
}
