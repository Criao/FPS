using System;

namespace FPSGame.Network
{
    /// <summary>
    /// 服务器配置
    /// </summary>
    [Serializable]
    public class ServerConfig
    {
        public string serverUrl = "https://api.example.com";
        public string cdnUrl = "https://cdn.example.com";
        public int timeout = 30;
    }

    /// <summary>
    /// 应用版本信息
    /// </summary>
    [Serializable]
    public class AppVersion
    {
        public string version = "1.0.0";
        public int buildNumber = 1;
    }
}
