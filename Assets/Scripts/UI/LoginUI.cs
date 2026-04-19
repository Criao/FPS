using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSGame.Utils;

namespace FPSGame.UI
{
    /// <summary>
    /// 登录界面UI控制器
    /// </summary>
    public class LoginUI : MonoBehaviour
    {
        [Header("UI引用")]
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public Toggle rememberMeToggle;
        public GameObject checkmarkImage; // 勾选框图标
        public Button loginButton;
        public Button guestLoginButton;
        public Button registerButton;
        public Button forgotPasswordButton;
        public TextMeshProUGUI messageText;

        [Header("面板引用")]
        public GameObject loginPanel;
        public GameObject registerPanel;
        public GameObject forgotPasswordPanel;

        private void Start()
        {
            // 确保LoginManager存在
            if (Login.LoginManager.Instance == null)
            {
                GameObject loginManagerObj = new GameObject("LoginManager");
                loginManagerObj.AddComponent<Login.LoginManager>();
                DontDestroyOnLoad(loginManagerObj);
            }

            // 检查热更新
            StartCoroutine(CheckHotUpdate());

            // 绑定按钮事件
            loginButton.onClick.AddListener(OnLoginButtonClick);
            guestLoginButton.onClick.AddListener(OnGuestLoginButtonClick);
            registerButton.onClick.AddListener(OnRegisterButtonClick);
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordButtonClick);

            // 绑定 Toggle 事件
            if (rememberMeToggle != null)
            {
                // 默认不勾选
                rememberMeToggle.isOn = false;
                rememberMeToggle.onValueChanged.AddListener(OnRememberMeToggleChanged);
                // 初始化勾选框显示状态
                UpdateCheckmarkVisibility(rememberMeToggle.isOn);
            }

            // 尝试加载记住的账号密码
            LoadRememberedCredentials();

            // 暂时禁用自动登录，避免报错
            // StartCoroutine(TryAutoLogin());
        }

        private System.Collections.IEnumerator CheckHotUpdate()
        {
            // 确保 HotUpdateManager 存在
            if (HotUpdate.HotUpdateManager.Instance == null)
            {
                GameObject hotUpdateObj = new GameObject("HotUpdateManager");
                hotUpdateObj.AddComponent<HotUpdate.HotUpdateManager>();
                DontDestroyOnLoad(hotUpdateObj);
            }

            ShowMessage("Checking for updates...", Color.yellow);

            yield return HotUpdate.HotUpdateManager.Instance.CheckForUpdates();

            if (HotUpdate.HotUpdateManager.Instance.HasUpdate)
            {
                ShowMessage("Downloading updates...", Color.yellow);

                yield return HotUpdate.HotUpdateManager.Instance.DownloadUpdates((progress) =>
                {
                    ShowMessage($"Downloading... {(int)(progress * 100)}%", Color.yellow);
                });

                ShowMessage("Update complete!", Color.green);
                yield return new WaitForSeconds(1f);
            }

            ShowMessage("", Color.white);
        }

        private System.Collections.IEnumerator TryAutoLogin()
        {
            yield return Login.LoginManager.Instance.TryAutoLogin((success) =>
            {
                if (success)
                {
                    ShowMessage("Auto login successful", Color.green);
                    Login.LoginManager.Instance.EnterGame();
                }
                else
                {
                    // Auto login failed, stay on login screen
                    Utils.Logger.Log("No saved login, showing login screen");
                }
            });
        }

        private void LoadRememberedCredentials()
        {
            string savedUsername = Login.TokenManager.Instance.GetRememberedUsername();
            string savedPassword = Login.TokenManager.Instance.GetRememberedPassword();

            if (!string.IsNullOrEmpty(savedUsername))
            {
                usernameInput.text = savedUsername;
                passwordInput.text = savedPassword;
                rememberMeToggle.isOn = true;
            }
        }

        private void OnLoginButtonClick()
        {
            string username = usernameInput.text.Trim();
            string password = passwordInput.text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowMessage("Please enter username and password", Color.red);
                return;
            }

            loginButton.interactable = false;
            ShowMessage("Logging in...", Color.yellow);

            Login.LoginManager.Instance.AccountLogin(username, password, rememberMeToggle.isOn, (success, message) =>
            {
                loginButton.interactable = true;

                if (success)
                {
                    ShowMessage(message, Color.green);
                    Login.LoginManager.Instance.EnterGame();
                }
                else
                {
                    ShowMessage(message, Color.red);
                }
            });
        }

        private void OnGuestLoginButtonClick()
        {
            guestLoginButton.interactable = false;
            ShowMessage("Guest login...", Color.yellow);

            Login.LoginManager.Instance.GuestLogin((success, message) =>
            {
                guestLoginButton.interactable = true;

                if (success)
                {
                    ShowMessage(message, Color.green);
                    Login.LoginManager.Instance.EnterGame();
                }
                else
                {
                    ShowMessage(message, Color.red);
                }
            });
        }

        private void OnRegisterButtonClick()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
        }

        private void OnForgotPasswordButtonClick()
        {
            loginPanel.SetActive(false);
            forgotPasswordPanel.SetActive(true);
        }

        private void OnRememberMeToggleChanged(bool isOn)
        {
            UpdateCheckmarkVisibility(isOn);
        }

        private void UpdateCheckmarkVisibility(bool show)
        {
            if (checkmarkImage != null)
            {
                checkmarkImage.SetActive(show);
            }
        }

        private void ShowMessage(string message, Color color)
        {
            messageText.text = message;
            messageText.color = color;
            Utils.Logger.Log($"UI Message: {message}");
        }

        public void ShowLoginPanel()
        {
            loginPanel.SetActive(true);
            registerPanel.SetActive(false);
            forgotPasswordPanel.SetActive(false);
        }
    }
}
