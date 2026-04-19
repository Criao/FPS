using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using FPSGame.Utils;

namespace FPSGame.Network
{
    /// <summary>
    /// 网络管理器，封装UnityWebRequest
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        private int timeout = 30;
        private int maxRetries = 3;

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

        public void Initialize()
        {
            Utils.Logger.Log("网络管理器初始化完成");
        }

        /// <summary>
        /// GET请求
        /// </summary>
        public IEnumerator Get(string url, Action<bool, string> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeout;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, request.downloadHandler.text);
                }
                else
                {
                    Utils.Logger.LogError($"GET请求失败: {url} - {request.error}");
                    callback?.Invoke(false, request.error);
                }
            }
        }

        /// <summary>
        /// POST请求（JSON格式）
        /// </summary>
        public IEnumerator Post(string url, string jsonData, Action<bool, string> callback)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeout;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, request.downloadHandler.text);
                }
                else
                {
                    Utils.Logger.LogError($"POST请求失败: {url} - {request.error}");
                    callback?.Invoke(false, request.error);
                }
            }
        }

        /// <summary>
        /// POST请求（带Token）
        /// </summary>
        public IEnumerator PostWithToken(string url, string jsonData, string token, Action<bool, string> callback)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                request.timeout = timeout;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, request.downloadHandler.text);
                }
                else
                {
                    Utils.Logger.LogError($"POST请求失败: {url} - {request.error}");
                    callback?.Invoke(false, request.error);
                }
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        public IEnumerator DownloadFile(string url, string savePath, Action<bool, string> callback, Action<float> progressCallback = null)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeout;
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    progressCallback?.Invoke(operation.progress);
                    yield return null;
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    System.IO.File.WriteAllBytes(savePath, request.downloadHandler.data);
                    callback?.Invoke(true, "下载成功");
                }
                else
                {
                    Utils.Logger.LogError($"文件下载失败: {url} - {request.error}");
                    callback?.Invoke(false, request.error);
                }
            }
        }
    }
}
