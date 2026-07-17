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
        /// 验证本地保存的 Token
        /// </summary>
        public IEnumerator VerifyToken(string token, Action<bool, string, UserData> callback)
        {
            var request = new VerifyTokenRequest { token = token };
            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/verify");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.PostWithToken(url, json, token, (success, data) =>
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
                    callback?.Invoke(true, response.message, response.data);
                }
                else
                {
                    callback?.Invoke(false, response?.message ?? "Token verification failed", null);
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}", null);
            }
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

        /// <summary>
        /// 重置密码
        /// </summary>
        public IEnumerator ResetPassword(string email, string resetCode, string newPassword, Action<bool, string> callback)
        {
            string passwordHash = Crypto.SHA256Hash(newPassword);
            var request = new ResetPasswordRequest
            {
                email = email,
                resetCode = resetCode,
                passwordHash = passwordHash
            };

            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/reset-password");

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
                    callback?.Invoke(false, response?.message ?? "Password reset failed");
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}");
            }
        }

        /// <summary>
        /// 注销当前后端会话
        /// </summary>
        public IEnumerator Logout(string token, Action<bool, string> callback)
        {
            if (string.IsNullOrEmpty(token))
            {
                callback?.Invoke(true, "Logged out");
                yield break;
            }

            var request = new VerifyTokenRequest { token = token };
            string json = JsonHelper.ToJson(request);
            string url = GetApiUrl("/api/auth/logout");

            bool requestComplete = false;
            bool requestSuccess = false;
            string responseData = "";

            yield return NetworkManager.Instance.PostWithToken(url, json, token, (success, data) =>
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
                    callback?.Invoke(false, response?.message ?? "Logout failed");
                }
            }
            else
            {
                callback?.Invoke(false, $"Network error: {responseData}");
            }
        }
    }
}
