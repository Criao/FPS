using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using FPSGame.Core;

namespace FPSGame.Network
{
    /// <summary>
    /// Wraps UnityWebRequest with consistent timeout, retry, and error handling.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [SerializeField] private int timeout = 30;
        [SerializeField] private int maxRetries = 3;

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
            if (ConfigManager.Instance != null && ConfigManager.Instance.ServerConfig != null)
            {
                timeout = Mathf.Max(1, ConfigManager.Instance.ServerConfig.timeout);
            }

            maxRetries = Mathf.Max(0, maxRetries);
            Utils.Logger.Log($"Network manager initialized. timeout={timeout}s retries={maxRetries}");
        }

        public IEnumerator Get(string url, Action<bool, string> callback)
        {
            yield return SendRequest(
                () => UnityWebRequest.Get(url),
                "GET",
                url,
                callback);
        }

        public IEnumerator Post(string url, string jsonData, Action<bool, string> callback)
        {
            yield return SendRequest(
                () => CreateJsonPostRequest(url, jsonData),
                "POST",
                url,
                callback);
        }

        public IEnumerator PostWithToken(string url, string jsonData, string token, Action<bool, string> callback)
        {
            yield return SendRequest(
                () => CreateJsonPostRequest(url, jsonData, token),
                "POST",
                url,
                callback);
        }

        private static UnityWebRequest CreateJsonPostRequest(string url, string jsonData, string token = null)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData ?? string.Empty);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");
            }

            return request;
        }

        private IEnumerator SendRequest(Func<UnityWebRequest> createRequest, string method, string url, Action<bool, string> callback)
        {
            int attempt = 0;
            string lastError = string.Empty;

            while (attempt <= maxRetries)
            {
                using (UnityWebRequest request = createRequest())
                {
                    request.timeout = timeout;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        callback?.Invoke(true, request.downloadHandler.text);
                        yield break;
                    }

                    lastError = GetErrorMessage(request);
                    if (ShouldRetry(request) && attempt < maxRetries)
                    {
                        attempt++;
                        Utils.Logger.LogWarning($"{method} request failed, retrying ({attempt}/{maxRetries}): {url} - {lastError}");
                        yield return new WaitForSeconds(GetRetryDelay(attempt));
                        continue;
                    }
                }

                Utils.Logger.LogError($"{method} request failed: {url} - {lastError}");
                callback?.Invoke(false, lastError);
                yield break;
            }

            callback?.Invoke(false, lastError);
        }

        private static string GetErrorMessage(UnityWebRequest request)
        {
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrEmpty(responseText))
            {
                return $"HTTP {request.responseCode}: {responseText}";
            }

            if (request.responseCode > 0)
            {
                return $"HTTP {request.responseCode}: {request.error}";
            }

            return request.error;
        }

        private static bool ShouldRetry(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return true;
            }

            return request.result == UnityWebRequest.Result.ProtocolError && request.responseCode >= 500;
        }

        private static float GetRetryDelay(int attempt)
        {
            return Mathf.Min(2f, 0.25f * attempt);
        }
    }
}
