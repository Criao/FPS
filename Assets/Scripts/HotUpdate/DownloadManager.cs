using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace FPSGame.HotUpdate
{
    public class DownloadManager : MonoBehaviour
    {
        private static DownloadManager instance;

        private readonly Queue<DownloadTask> downloadQueue = new Queue<DownloadTask>();
        private DownloadTask currentTask;
        private bool isDownloading;

        public int MaxRetryCount = 3;
        public float TimeoutSeconds = 30f;

        public static DownloadManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("DownloadManager");
                    instance = go.AddComponent<DownloadManager>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

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

        public void AddDownload(
            string url,
            string savePath,
            string expectedHash,
            Action<bool, string> onComplete,
            Action<float> onProgress = null)
        {
            DownloadTask task = new DownloadTask
            {
                Url = url,
                SavePath = savePath,
                ExpectedHash = expectedHash,
                OnComplete = onComplete,
                OnProgress = onProgress
            };

            downloadQueue.Enqueue(task);

            if (!isDownloading)
            {
                StartCoroutine(ProcessDownloadQueue());
            }
        }

        public static string CalculateMD5(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            using (MD5 md5 = MD5.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
            EnsureDirectoryExists(task.SavePath);

            if (File.Exists(task.SavePath) && HashMatches(CalculateMD5(task.SavePath), task.ExpectedHash))
            {
                Utils.Logger.Log($"File already exists and verified: {Path.GetFileName(task.SavePath)}");
                task.OnComplete?.Invoke(true, "File already exists");
                yield break;
            }

            if (File.Exists(task.SavePath))
            {
                File.Delete(task.SavePath);
            }

            string tempPath = task.SavePath + ".download";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Utils.Logger.Log($"Downloading: {task.Url}");

            UnityWebRequest request = UnityWebRequest.Get(task.Url);
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.SetRequestHeader("Pragma", "no-cache");
            request.downloadHandler = new DownloadHandlerFile(tempPath);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(TimeoutSeconds));

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                task.OnProgress?.Invoke(request.downloadProgress);
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                CompleteSuccessfulRequest(task, tempPath);
            }
            else
            {
                CompleteFailedRequest(task, tempPath, request.error);
            }

            request.Dispose();
        }

        private void CompleteSuccessfulRequest(DownloadTask task, string tempPath)
        {
            string fileHash = CalculateMD5(tempPath);
            long fileSize = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

            if (!HashMatches(fileHash, task.ExpectedHash))
            {
                Utils.Logger.LogError(
                    $"Hash mismatch: {Path.GetFileName(task.SavePath)} expected={task.ExpectedHash} actual={fileHash} bytes={fileSize}");

                DeleteIfExists(tempPath);
                RetryOrFail(task, "Hash verification failed");
                return;
            }

            DeleteIfExists(task.SavePath);
            File.Move(tempPath, task.SavePath);
            Utils.Logger.Log($"Download successful: {Path.GetFileName(task.SavePath)}");
            task.OnComplete?.Invoke(true, "Download successful");
        }

        private void CompleteFailedRequest(DownloadTask task, string tempPath, string error)
        {
            Utils.Logger.LogError($"Download failed: {error}");
            DeleteIfExists(tempPath);
            RetryOrFail(task, error);
        }

        private void RetryOrFail(DownloadTask task, string message)
        {
            if (task.RetryCount < MaxRetryCount)
            {
                task.RetryCount++;
                Utils.Logger.Log($"Retrying download ({task.RetryCount}/{MaxRetryCount})...");
                downloadQueue.Enqueue(task);
                return;
            }

            task.OnComplete?.Invoke(false, message);
        }

        private static void EnsureDirectoryExists(string savePath)
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool HashMatches(string actualHash, string expectedHash)
        {
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private sealed class DownloadTask
        {
            public string Url;
            public string SavePath;
            public string ExpectedHash;
            public int RetryCount;
            public Action<bool, string> OnComplete;
            public Action<float> OnProgress;
        }
    }
}
