using System;
using System.Collections;
using UnityEngine;
using FPSGame.Core;
using FPSGame.Utils;

namespace FPSGame.Login
{
    /// <summary>
    /// 登录管理器，处理所有登录相关逻辑
    /// </summary>
    public class LoginManager : MonoBehaviour
    {
        public static LoginManager Instance { get; private set; }

        public UserData CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null && !string.IsNullOrEmpty(CurrentUser.token);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 确保 ConfigManager 存在
                if (ConfigManager.Instance == null)
                {
                    GameObject configManagerObj = new GameObject("ConfigManager");
                    configManagerObj.AddComponent<ConfigManager>();
                    DontDestroyOnLoad(configManagerObj);
                }

                // 确保 NetworkManager 存在
                if (Network.NetworkManager.Instance == null)
                {
                    GameObject networkManagerObj = new GameObject("NetworkManager");
                    networkManagerObj.AddComponent<Network.NetworkManager>();
                    DontDestroyOnLoad(networkManagerObj);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 尝试自动登录
        /// </summary>
        public IEnumerator TryAutoLogin(Action<bool> callback)
        {
            if (!TokenManager.Instance.HasRememberedToken())
            {
                if (TokenManager.Instance.HasToken())
                {
                    TokenManager.Instance.ClearToken();
                }

                callback?.Invoke(false);
                yield break;
            }

            string token = TokenManager.Instance.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                TokenManager.Instance.ClearToken();
                callback?.Invoke(false);
                yield break;
            }

            bool requestComplete = false;
            bool requestSuccess = false;

            yield return AuthService.Instance.VerifyToken(token, (success, message, userData) =>
            {
                requestComplete = true;
                requestSuccess = success;

                if (success)
                {
                    CurrentUser = userData;
                    TokenManager.Instance.SaveToken(
                        userData.token,
                        userData.userId,
                        userData.username,
                        userData.isGuest,
                        !userData.isGuest);
                    Utils.Logger.Log($"自动登录成功: {userData.username}");
                }
                else
                {
                    TokenManager.Instance.ClearToken();
                    Utils.Logger.Log($"自动登录失败: {message}");
                }
            });

            yield return new WaitUntil(() => requestComplete);
            callback?.Invoke(requestSuccess);
        }

        /// <summary>
        /// 游客登录
        /// </summary>
        public void GuestLogin(Action<bool, string> callback)
        {
            StartCoroutine(GuestLoginCoroutine(callback));
        }

        private IEnumerator GuestLoginCoroutine(Action<bool, string> callback)
        {
            Utils.Logger.Log("开始游客登录");

            yield return AuthService.Instance.GuestLogin((success, message, userData) =>
            {
                if (success)
                {
                    CurrentUser = userData;
                    TokenManager.Instance.SaveToken(userData.token, userData.userId, userData.username, userData.isGuest, false);
                    Utils.Logger.Log($"游客登录成功: {userData.username}");
                }
                callback?.Invoke(success, message);
            });
        }

        /// <summary>
        /// 账号登录
        /// </summary>
        public void AccountLogin(string username, string password, bool rememberMe, Action<bool, string> callback)
        {
            StartCoroutine(AccountLoginCoroutine(username, password, rememberMe, callback));
        }

        private IEnumerator AccountLoginCoroutine(string username, string password, bool rememberMe, Action<bool, string> callback)
        {
            Utils.Logger.Log($"开始账号登录: {username}");

            yield return AuthService.Instance.AccountLogin(username, password, (success, message, userData) =>
            {
                if (success)
                {
                    CurrentUser = userData;

                    if (rememberMe)
                    {
                        TokenManager.Instance.SaveToken(userData.token, userData.userId, userData.username, userData.isGuest, true);
                        Utils.Logger.Log("已保存登录信息");
                    }
                    else
                    {
                        TokenManager.Instance.ClearToken();
                        TokenManager.Instance.ClearRememberedLogin();
                    }

                    Utils.Logger.Log($"账号登录成功: {userData.username}");
                }
                callback?.Invoke(success, message);
            });
        }

        /// <summary>
        /// 注册账号
        /// </summary>
        public void Register(string username, string email, string password, Action<bool, string> callback)
        {
            StartCoroutine(RegisterCoroutine(username, email, password, callback));
        }

        private IEnumerator RegisterCoroutine(string username, string email, string password, Action<bool, string> callback)
        {
            Utils.Logger.Log($"开始注册账号: {username}");

            yield return AuthService.Instance.Register(username, email, password, (success, message) =>
            {
                if (success)
                {
                    Utils.Logger.Log("注册成功");
                }
                callback?.Invoke(success, message);
            });
        }

        /// <summary>
        /// 找回密码
        /// </summary>
        public void ForgotPassword(string email, Action<bool, string> callback)
        {
            StartCoroutine(ForgotPasswordCoroutine(email, callback));
        }

        private IEnumerator ForgotPasswordCoroutine(string email, Action<bool, string> callback)
        {
            Utils.Logger.Log($"请求找回密码: {email}");

            yield return AuthService.Instance.ForgotPassword(email, (success, message) =>
            {
                callback?.Invoke(success, message);
            });
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        public void ResetPassword(string email, string resetCode, string newPassword, Action<bool, string> callback)
        {
            StartCoroutine(ResetPasswordCoroutine(email, resetCode, newPassword, callback));
        }

        private IEnumerator ResetPasswordCoroutine(string email, string resetCode, string newPassword, Action<bool, string> callback)
        {
            Utils.Logger.Log($"重置密码: {email}");

            yield return AuthService.Instance.ResetPassword(email, resetCode, newPassword, (success, message) =>
            {
                if (success)
                {
                    TokenManager.Instance.ClearToken();
                }

                callback?.Invoke(success, message);
            });
        }

        /// <summary>
        /// 登出
        /// </summary>
        public void Logout()
        {
            string token = TokenManager.Instance.GetToken();

            CurrentUser = null;
            TokenManager.Instance.ClearToken();
            Utils.Logger.Log("已登出");

            if (!string.IsNullOrEmpty(token))
            {
                StartCoroutine(AuthService.Instance.Logout(token, (success, message) =>
                {
                    if (!success)
                    {
                        Utils.Logger.LogWarning($"Server logout failed: {message}");
                    }
                }));
            }
        }

        /// <summary>
        /// 进入游戏
        /// </summary>
        public void EnterGame()
        {
            if (IsLoggedIn)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EnterGame();
                }
                else
                {
                    // 如果没有 GameManager，直接加载游戏场景
                    Utils.Logger.Log("Loading game scene directly (no GameManager)");
                    UnityEngine.SceneManagement.SceneManager.LoadScene("FPS");
                }
            }
            else
            {
                Utils.Logger.LogError("Not logged in, cannot enter game");
            }
        }
    }
}
