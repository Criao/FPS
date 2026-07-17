using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FPSGame.HotUpdate;
using FPSGame.Network;
using FPSGame.Utils;

namespace FPSGame.Core
{
    /// <summary>
    /// 游戏总管理器，控制游戏启动流程和状态机
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Initializing,    // 初始化
            CheckingUpdate,  // 检查更新
            Downloading,     // 下载资源
            Login,           // 登录
            InGame           // 游戏中
        }

        public GameState CurrentState { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            Utils.Logger.Log("游戏管理器初始化开始");

            // 确保ConfigManager存在
            if (ConfigManager.Instance == null)
            {
                GameObject configObj = new GameObject("ConfigManager");
                configObj.AddComponent<ConfigManager>();
                DontDestroyOnLoad(configObj);
            }

            // 确保NetworkManager存在
            if (NetworkManager.Instance == null)
            {
                GameObject networkObj = new GameObject("NetworkManager");
                networkObj.AddComponent<NetworkManager>();
                DontDestroyOnLoad(networkObj);
            }

            // 确保HotUpdateManager存在
            if (HotUpdateManager.Instance == null)
            {
                GameObject hotUpdateObj = new GameObject("HotUpdateManager");
                hotUpdateObj.AddComponent<HotUpdateManager>();
                DontDestroyOnLoad(hotUpdateObj);
            }

            // 初始化各个管理器
            ConfigManager.Instance.LoadConfig();
            NetworkManager.Instance.Initialize();

            // 开始启动流程
            StartCoroutine(StartupSequence());
        }

        private IEnumerator StartupSequence()
        {
            CurrentState = GameState.Initializing;
            Utils.Logger.Log("启动流程开始");

            yield return new WaitForSeconds(0.5f);

            // 1. 检查热更新
            CurrentState = GameState.CheckingUpdate;
            Utils.Logger.Log("检查热更新...");
            yield return HotUpdateManager.Instance.CheckForUpdates();

            // 2. 如果有更新，下载资源
            if (HotUpdateManager.Instance.HasUpdate)
            {
                CurrentState = GameState.Downloading;
                Utils.Logger.Log("开始下载更新...");
                yield return HotUpdateManager.Instance.DownloadUpdates();

                if (!HotUpdateManager.Instance.LastDownloadSucceeded)
                {
                    Utils.Logger.LogWarning("热更新下载失败，继续进入登录流程");
                }
            }
            else
            {
                Utils.Logger.Log("无需更新");
            }

            // 3. 进入登录流程
            CurrentState = GameState.Login;
            if (SceneManager.GetActiveScene().name != "Login")
            {
                Utils.Logger.Log("进入登录场景");
                SceneManager.LoadScene("Login");
            }
            else
            {
                Utils.Logger.Log("当前已在登录场景");
            }
        }

        /// <summary>
        /// 进入游戏主场景
        /// </summary>
        public void EnterGame()
        {
            CurrentState = GameState.InGame;
            Utils.Logger.Log("进入游戏主场景");
            SceneManager.LoadScene("FPS");
        }
    }
}
