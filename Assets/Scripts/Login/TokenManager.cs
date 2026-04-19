using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Login
{
    /// <summary>
    /// Token管理器，负责Token的加密存储和读取
    /// </summary>
    public class TokenManager
    {
        private static TokenManager instance;
        public static TokenManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new TokenManager();
                return instance;
            }
        }

        private const string TOKEN_KEY = "user_token";
        private const string USER_ID_KEY = "user_id";
        private const string USERNAME_KEY = "username";
        private const string IS_GUEST_KEY = "is_guest";

        /// <summary>
        /// 保存Token
        /// </summary>
        public void SaveToken(string token, string userId, string username, bool isGuest)
        {
            string encryptedToken = Crypto.AESEncrypt(token);
            PlayerPrefs.SetString(TOKEN_KEY, encryptedToken);
            PlayerPrefs.SetString(USER_ID_KEY, userId);
            PlayerPrefs.SetString(USERNAME_KEY, username);
            PlayerPrefs.SetInt(IS_GUEST_KEY, isGuest ? 1 : 0);
            PlayerPrefs.Save();

            Utils.Logger.Log("Token已保存");
        }

        /// <summary>
        /// 获取Token
        /// </summary>
        public string GetToken()
        {
            if (!PlayerPrefs.HasKey(TOKEN_KEY))
                return string.Empty;

            string encryptedToken = PlayerPrefs.GetString(TOKEN_KEY);
            return Crypto.AESDecrypt(encryptedToken);
        }

        /// <summary>
        /// 获取用户ID
        /// </summary>
        public string GetUserId()
        {
            return PlayerPrefs.GetString(USER_ID_KEY, string.Empty);
        }

        /// <summary>
        /// 获取用户名
        /// </summary>
        public string GetUsername()
        {
            return PlayerPrefs.GetString(USERNAME_KEY, string.Empty);
        }

        /// <summary>
        /// 是否是游客
        /// </summary>
        public bool IsGuest()
        {
            return PlayerPrefs.GetInt(IS_GUEST_KEY, 0) == 1;
        }

        /// <summary>
        /// 是否有保存的Token
        /// </summary>
        public bool HasToken()
        {
            return PlayerPrefs.HasKey(TOKEN_KEY);
        }

        /// <summary>
        /// 清除Token（登出）
        /// </summary>
        public void ClearToken()
        {
            PlayerPrefs.DeleteKey(TOKEN_KEY);
            PlayerPrefs.DeleteKey(USER_ID_KEY);
            PlayerPrefs.DeleteKey(USERNAME_KEY);
            PlayerPrefs.DeleteKey(IS_GUEST_KEY);
            PlayerPrefs.Save();

            Utils.Logger.Log("Token已清除");
        }

        /// <summary>
        /// 保存记住的密码
        /// </summary>
        public void SaveRememberedPassword(string username, string password)
        {
            string encryptedPassword = Crypto.AESEncrypt(password);
            PlayerPrefs.SetString("saved_username", username);
            PlayerPrefs.SetString("saved_password", encryptedPassword);
            PlayerPrefs.SetInt("remember_me", 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 获取记住的用户名
        /// </summary>
        public string GetRememberedUsername()
        {
            if (PlayerPrefs.GetInt("remember_me", 0) == 1)
                return PlayerPrefs.GetString("saved_username", string.Empty);
            return string.Empty;
        }

        /// <summary>
        /// 获取记住的密码
        /// </summary>
        public string GetRememberedPassword()
        {
            if (PlayerPrefs.GetInt("remember_me", 0) == 1)
            {
                string encryptedPassword = PlayerPrefs.GetString("saved_password", string.Empty);
                return Crypto.AESDecrypt(encryptedPassword);
            }
            return string.Empty;
        }

        /// <summary>
        /// 清除记住的密码
        /// </summary>
        public void ClearRememberedPassword()
        {
            PlayerPrefs.DeleteKey("saved_username");
            PlayerPrefs.DeleteKey("saved_password");
            PlayerPrefs.DeleteKey("remember_me");
            PlayerPrefs.Save();
        }
    }
}
