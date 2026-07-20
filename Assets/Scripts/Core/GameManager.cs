using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FPSGame.HotUpdate;
using FPSGame.Network;
using FPSGame.UI;
using FPSGame.Utils;

namespace FPSGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Initializing,
            CheckingUpdate,
            Downloading,
            Login,
            InGame
        }

        public GameState CurrentState { get; private set; }

        private Coroutine startupCoroutine;
        private Coroutine sceneLoadCoroutine;
        private HotUpdateUI hotUpdateUI;

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
            Utils.Logger.Log("GameManager initializing");

            if (ConfigManager.Instance == null)
            {
                GameObject configObj = new GameObject("ConfigManager");
                configObj.AddComponent<ConfigManager>();
                DontDestroyOnLoad(configObj);
            }

            if (NetworkManager.Instance == null)
            {
                GameObject networkObj = new GameObject("NetworkManager");
                networkObj.AddComponent<NetworkManager>();
                DontDestroyOnLoad(networkObj);
            }

            if (HotUpdateManager.Instance == null)
            {
                GameObject hotUpdateObj = new GameObject("HotUpdateManager");
                hotUpdateObj.AddComponent<HotUpdateManager>();
                DontDestroyOnLoad(hotUpdateObj);
            }

            hotUpdateUI = HotUpdateUI.GetOrCreate();

            ConfigManager.Instance.LoadConfig();
            NetworkManager.Instance.Initialize();

            startupCoroutine = StartCoroutine(StartupSequence());
        }

        private IEnumerator StartupSequence()
        {
            CurrentState = GameState.Initializing;
            Utils.Logger.Log("Startup sequence started");

            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(RunHotUpdateFlow());

            if (CurrentState == GameState.InGame)
            {
                startupCoroutine = null;
                yield break;
            }

            LoadLoginScene();
            startupCoroutine = null;
        }

        private IEnumerator RunHotUpdateFlow()
        {
            if (hotUpdateUI == null)
            {
                Utils.Logger.LogError("HotUpdateUI is missing. Hot update UI will be skipped.");
                yield break;
            }

            while (true)
            {
                CurrentState = GameState.CheckingUpdate;
                hotUpdateUI.ShowChecking(CreateLocalVersionInfo());
                Utils.Logger.Log("Checking hot updates");

                yield return HotUpdateManager.Instance.CheckForUpdates();

                if (!HotUpdateManager.Instance.LastCheckSucceeded)
                {
                    yield return StartCoroutine(WaitForFailureDecision(
                        "Update Check Failed",
                        HotUpdateManager.Instance.LastErrorMessage,
                        false));

                    if (hotUpdateUI.Decision == UpdateUiDecision.Retry)
                    {
                        continue;
                    }

                    if (hotUpdateUI.Decision == UpdateUiDecision.Exit)
                    {
                        QuitApplication();
                    }

                    yield break;
                }

                if (!HotUpdateManager.Instance.HasUpdate)
                {
                    Utils.Logger.Log("No hot update required");
                    hotUpdateUI.ShowReady(
                        "No Update Needed",
                        GetVersionDetail(HotUpdateManager.Instance.LocalVersion));
                    yield return StartCoroutine(WaitForUiDecision());
                    yield break;
                }

                VersionInfo remoteVersion = HotUpdateManager.Instance.RemoteVersion;
                hotUpdateUI.ShowUpdateFound(HotUpdateManager.Instance.LocalVersion, remoteVersion);
                yield return new WaitForSeconds(0.35f);

                CurrentState = GameState.Downloading;
                Utils.Logger.Log("Downloading hot updates");
                hotUpdateUI.ShowDownloading(remoteVersion, 0f);
                yield return HotUpdateManager.Instance.DownloadUpdates(progress =>
                {
                    hotUpdateUI.ShowDownloading(remoteVersion, progress);
                });

                if (HotUpdateManager.Instance.LastDownloadSucceeded)
                {
                    hotUpdateUI.ShowReady("Update Complete", GetVersionDetail(HotUpdateManager.Instance.LocalVersion));
                    yield return StartCoroutine(WaitForUiDecision());
                    yield break;
                }

                bool forceUpdate = remoteVersion != null && remoteVersion.forceUpdate;
                yield return StartCoroutine(WaitForFailureDecision(
                    "Update Download Failed",
                    HotUpdateManager.Instance.LastErrorMessage,
                    forceUpdate));

                if (hotUpdateUI.Decision == UpdateUiDecision.Retry)
                {
                    continue;
                }

                if (hotUpdateUI.Decision == UpdateUiDecision.Exit)
                {
                    QuitApplication();
                }

                yield break;
            }
        }

        private IEnumerator WaitForFailureDecision(string title, string detail, bool forceUpdate)
        {
            hotUpdateUI.ShowFailure(title, detail, forceUpdate, !forceUpdate);
            yield return StartCoroutine(WaitForUiDecision());
        }

        private IEnumerator WaitForUiDecision()
        {
            if (hotUpdateUI == null)
            {
                yield break;
            }

            while (hotUpdateUI.Decision == UpdateUiDecision.None)
            {
                yield return null;
            }
        }

        private void LoadLoginScene()
        {
            CurrentState = GameState.Login;
            if (hotUpdateUI != null)
            {
                hotUpdateUI.Hide();
            }

            if (SceneManager.GetActiveScene().name != "Login")
            {
                Utils.Logger.Log("Loading login scene");
                SceneManager.LoadScene("Login");
            }
            else
            {
                Utils.Logger.Log("Already in login scene");
            }
        }

        public void EnterGame()
        {
            if (startupCoroutine != null)
            {
                StopCoroutine(startupCoroutine);
                startupCoroutine = null;
            }

            if (hotUpdateUI != null)
            {
                hotUpdateUI.Hide();
            }

            CurrentState = GameState.InGame;
            Utils.Logger.Log("Entering game scene");

            if (sceneLoadCoroutine != null)
            {
                StopCoroutine(sceneLoadCoroutine);
            }

            sceneLoadCoroutine = StartCoroutine(LoadSceneAsync("FPS"));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                SceneManager.LoadScene(sceneName);
                sceneLoadCoroutine = null;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            sceneLoadCoroutine = null;
        }

        private VersionInfo CreateLocalVersionInfo()
        {
            AppVersion appVersion = ConfigManager.Instance != null
                ? ConfigManager.Instance.AppVersion
                : null;

            return new VersionInfo
            {
                version = appVersion != null ? appVersion.version : "unknown",
                buildNumber = appVersion != null ? appVersion.buildNumber : 0
            };
        }

        private static string GetVersionDetail(VersionInfo versionInfo)
        {
            if (versionInfo == null)
            {
                return "Version: unknown";
            }

            return $"Version: {versionInfo.version} (Build {versionInfo.buildNumber})";
        }

        private void QuitApplication()
        {
            Utils.Logger.Log("Exit requested from update UI");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
