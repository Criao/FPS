using System;

namespace FPSGame.Login
{
    /// <summary>
    /// 用户数据模型
    /// </summary>
    [Serializable]
    public class UserData
    {
        public string userId;
        public string username;
        public string email;
        public string token;
        public long tokenExpireTime;
        public bool isGuest;
    }

    /// <summary>
    /// 登录请求
    /// </summary>
    [Serializable]
    public class LoginRequest
    {
        public string username;
        public string passwordHash;
        public string deviceId;
    }

    /// <summary>
    /// 注册请求
    /// </summary>
    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string email;
        public string passwordHash;
        public string deviceId;
    }

    /// <summary>
    /// 游客登录请求
    /// </summary>
    [Serializable]
    public class GuestLoginRequest
    {
        public string deviceId;
    }

    /// <summary>
    /// 找回密码请求
    /// </summary>
    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }

    /// <summary>
    /// 登录响应
    /// </summary>
    [Serializable]
    public class LoginResponse
    {
        public bool success;
        public string message;
        public UserData data;
    }

    /// <summary>
    /// 通用响应
    /// </summary>
    [Serializable]
    public class CommonResponse
    {
        public bool success;
        public string message;
    }
}
