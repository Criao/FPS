using System;
using System.Collections;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Network;
using FPSGame.Utils;

namespace FPSGame.Login
{
    /// <summary>
    /// 认证服务，封装所有登录相关的API调用
    /// </summary>
    public class AuthService
    {
        private static AuthService instance;
        public static AuthService Instance
        {
            get
            {
                if (instance == null)
                    instance = new AuthService();
                return instance;
            }
        }

        private string GetApiUrl(string endpoint)
        {
            return $"{ConfigManager.Instance.ServerConfig.serverUrl}{endpoint}";
        }

        /// <summary>
        /// 游客登录
        /// </summary>
        public IEnumerator GuestLogin(Action<bool, string, UserData> callback)
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            var request = new GuestLoginRequest { deviceId = deviceId };
            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/guest");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.Post(url, json, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            });

            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                var response = JsonHelper.FromJson<LoginResponse>(responseData);
                if (response != null && response.success)
                {
                    callback?.Invoke(true, "Guest login successful", response.data);
                }
                else
                {
                    callback?.Invoke(false, response?.message ?? "Login failed", null);
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}", null);
            }
        }

        /// <summary>
        /// 账号登录
        /// </summary>
        public IEnumerator AccountLogin(string username, string password, Action<bool, string, UserData> callback)
        {
            string passwordHash = Crypto.SHA256Hash(password);
            string deviceId = SystemInfo.deviceUniqueIdentifier;

            var request = new LoginRequest
            {
                username = username,
                passwordHash = passwordHash,
                deviceId = deviceId
            };

            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/login");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.Post(url, json, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            });

            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                var response = JsonHelper.FromJson<LoginResponse>(responseData);
                if (response != null && response.success)
                {
                    callback?.Invoke(true, "Login successful", response.data);
                }
                else
                {
                    callback?.Invoke(false, response?.message ?? "Login failed", null);
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}", null);
            }
        }

        /// <summary>
        /// 注册账号
        /// </summary>
        public IEnumerator Register(string username, string email, string password, Action<bool, string> callback)
        {
            string passwordHash = Crypto.SHA256Hash(password);
            string deviceId = SystemInfo.deviceUniqueIdentifier;

            var request = new RegisterRequest
            {
                username = username,
                email = email,
                passwordHash = passwordHash,
                deviceId = deviceId
            };

            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/register");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.Post(url, json, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            });

            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                var response = JsonHelper.FromJson<CommonResponse>(responseData);
                if (response != null && response.success)
                {
                    callback?.Invoke(true, response.message);
                }
                else
                {
                    callback?.Invoke(false, response?.message ?? "Registration failed");
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}");
            }
        }

        /// <summary>
        /// 找回密码
        /// </summary>
        public IEnumerator ForgotPassword(string email, Action<bool, string> callback)
        {
            var request = new ForgotPasswordRequest { email = email };
            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/forgot-password");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.Post(url, json, (success, data) =>
            {
                requestComplete = true;
                requestSuccess = success;
                responseData = data;
            });

            yield return new WaitUntil(() => requestComplete);

            if (requestSuccess)
            {
                var response = JsonHelper.FromJson<CommonResponse>(responseData);
                if (response != null && response.success)
                {
                    callback?.Invoke(true, response.message);
                }
                else
                {
                    callback?.Invoke(false, response?.message ?? "Request failed");
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}");
            }
        }
    }
}
