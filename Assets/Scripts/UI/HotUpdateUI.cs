using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FPSGame.HotUpdate;

namespace FPSGame.UI
{
    public enum UpdateUiDecision
    {
        None,
        Retry,
        Continue,
        Exit
    }

    public class HotUpdateUI : MonoBehaviour
    {
        public static HotUpdateUI Instance { get; private set; }

        [Header("Scene UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button exitButton;

        public UpdateUiDecision Decision { get; private set; }

        public static HotUpdateUI GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            HotUpdateUI ui = FindObjectOfType<HotUpdateUI>(true);
            if (ui == null)
            {
                Debug.LogError("HotUpdateUI is missing from the scene. Add the HotUpdateUI canvas to the startup scene.");
                return null;
            }

            ui.Initialize();
            return ui;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Initialize();
            DontDestroyOnLoad(gameObject);
        }

        private void Initialize()
        {
            if (Instance == this)
            {
                return;
            }

            Instance = this;
            EnsureEventSystem();
            BindButtons();
            Hide();
        }

        public void ShowChecking(VersionInfo localVersion)
        {
            Show();
            Decision = UpdateUiDecision.None;
            SetTexts(
                "Checking Updates",
                "Checking version and patch files...",
                localVersion != null
                    ? $"Current version: {localVersion.version} (Build {localVersion.buildNumber})"
                    : "Current version: unknown");
            SetProgress(0f, false);
            SetButtons(false, false, false);
        }

        public void ShowUpdateFound(VersionInfo localVersion, VersionInfo remoteVersion)
        {
            Show();
            Decision = UpdateUiDecision.None;

            string updateMode = remoteVersion != null && remoteVersion.forceUpdate
                ? "Required update"
                : "Update available";

            string detail = remoteVersion != null
                ? $"{updateMode}: {remoteVersion.version} (Build {remoteVersion.buildNumber})\n{remoteVersion.updateDescription}"
                : updateMode;

            if (localVersion != null)
            {
                detail += $"\nCurrent version: {localVersion.version} (Build {localVersion.buildNumber})";
            }

            SetTexts("Update Found", "Preparing download...", detail);
            SetProgress(0f, true);
            SetButtons(false, false, false);
        }

        public void ShowDownloading(VersionInfo remoteVersion, float progress)
        {
            Show();
            Decision = UpdateUiDecision.None;

            string version = remoteVersion != null ? remoteVersion.version : "latest";
            SetTexts("Downloading Update", $"Downloading version {version}...", "Keep the game open while files are being verified.");
            SetProgress(progress, true);
            SetButtons(false, false, false);
        }

        public void ShowReady(string title, string detail)
        {
            Show();
            Decision = UpdateUiDecision.None;
            SetTexts(title, "Ready to enter the game.", detail);
            SetProgress(1f, true);
            SetButtons(false, true, false);
        }

        public void ShowFailure(string title, string detail, bool forceUpdate, bool canContinue)
        {
            Show();
            Decision = UpdateUiDecision.None;

            string status = forceUpdate
                ? "This update is required before entering the game."
                : "You can retry or continue with the current local version.";

            SetTexts(title, status, string.IsNullOrEmpty(detail) ? "No detailed error message." : detail);
            SetProgress(0f, false);
            SetButtons(true, canContinue, forceUpdate);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            SetCanvasReceivesInput(false);
        }

        private void Show()
        {
            SetCanvasReceivesInput(true);
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        private void SetCanvasReceivesInput(bool receivesInput)
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (canvas != null)
            {
                canvas.enabled = receivesInput;
            }

            GraphicRaycaster graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (graphicRaycaster != null)
            {
                graphicRaycaster.enabled = receivesInput;
            }
        }

        private void BindButtons()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryButtonClick);
                retryButton.onClick.AddListener(OnRetryButtonClick);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueButtonClick);
                continueButton.onClick.AddListener(OnContinueButtonClick);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClick);
                exitButton.onClick.AddListener(OnExitButtonClick);
            }
        }

        private void OnRetryButtonClick()
        {
            Decision = UpdateUiDecision.Retry;
        }

        private void OnContinueButtonClick()
        {
            Decision = UpdateUiDecision.Continue;
        }

        private void OnExitButtonClick()
        {
            Decision = UpdateUiDecision.Exit;
        }

        private void SetTexts(string title, string status, string detail)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (statusText != null)
            {
                statusText.text = status;
            }

            if (detailText != null)
            {
                detailText.text = detail;
            }
        }

        private void SetProgress(float progress, bool visible)
        {
            float clampedProgress = Mathf.Clamp01(progress);

            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(visible);
                progressSlider.value = clampedProgress;
            }

            if (progressText != null)
            {
                progressText.gameObject.SetActive(visible);
                progressText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
            }
        }

        private void SetButtons(bool showRetry, bool showContinue, bool showExit)
        {
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(showRetry);
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(showContinue);
            }

            if (exitButton != null)
            {
                exitButton.gameObject.SetActive(showExit);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }
    }
}
