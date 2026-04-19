using System.IO;
using UnityEngine;
using FPSGame.Network;
using FPSGame.Utils;

namespace FPSGame.Core
{
    /// <summary>
    /// 配置管理器，负责加载和管理配置文件
    /// </summary>
    public class ConfigManager : MonoBehaviour
    {
        public static ConfigManager Instance { get; private set; }

        public ServerConfig ServerConfig { get; private set; }
        public AppVersion AppVersion { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadConfig();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 加载所有配置文件
        /// </summary>
        public void LoadConfig()
        {
            LoadServerConfig();
            LoadAppVersion();
        }

        private void LoadServerConfig()
        {
            TextAsset configAsset = Resources.Load<TextAsset>("Config/ServerConfig");
            if (configAsset != null)
            {
                ServerConfig = JsonHelper.FromJson<ServerConfig>(configAsset.text);
                Utils.Logger.Log($"服务器配置加载成功: {ServerConfig.serverUrl}");
            }
            else
            {
                ServerConfig = new ServerConfig();
                Utils.Logger.LogWarning("未找到ServerConfig.json，使用默认配置");
            }
        }

        private void LoadAppVersion()
        {
            TextAsset versionAsset = Resources.Load<TextAsset>("Config/AppVersion");
            if (versionAsset != null)
            {
                AppVersion = JsonHelper.FromJson<AppVersion>(versionAsset.text);
                Utils.Logger.Log($"应用版本: {AppVersion.version} (Build {AppVersion.buildNumber})");
            }
            else
            {
                AppVersion = new AppVersion();
                Utils.Logger.LogWarning("未找到AppVersion.json，使用默认版本");
            }
        }

        /// <summary>
        /// 保存版本信息（热更新后更新本地版本号）
        /// </summary>
        public void SaveAppVersion(string version, int buildNumber)
        {
            AppVersion.version = version;
            AppVersion.buildNumber = buildNumber;

            string json = JsonHelper.ToJson(AppVersion, true);
            string path = Path.Combine(Application.persistentDataPath, "AppVersion.json");
            File.WriteAllText(path, json);
            Utils.Logger.Log($"版本信息已保存: {version}");
        }
    }
}
