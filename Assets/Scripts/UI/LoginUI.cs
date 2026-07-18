using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using FPSGame.Core;

namespace FPSGame.UI
{
    public class LoginUI : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        public Toggle rememberMeToggle;
        public GameObject checkmarkImage;
        public Button loginButton;
        public Button guestLoginButton;
        public Button registerButton;
        public Button forgotPasswordButton;
        public TextMeshProUGUI messageText;

        [Header("Panel References")]
        public GameObject loginPanel;
        public GameObject registerPanel;
        public GameObject forgotPasswordPanel;

        private bool buttonsBound;

        private void OnEnable()
        {
            ReleaseCursorForUi();
        }

        private void Start()
        {
            ReleaseCursorForUi();
            EnsureEventSystem();
            EnsureLoginManager();
            DisablePanelBackgroundRaycasts();

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            BindButtons();
            InitializeRememberMe();
            LoadRememberedCredentials();
            StartCoroutine(TryAutoLogin());
        }

        private void EnsureLoginManager()
        {
            if (Login.LoginManager.Instance != null)
            {
                return;
            }

            GameObject loginManagerObj = new GameObject("LoginManager");
            loginManagerObj.AddComponent<Login.LoginManager>();
            DontDestroyOnLoad(loginManagerObj);
        }

        private bool ValidateReferences()
        {
            bool valid =
                usernameInput != null &&
                passwordInput != null &&
                loginButton != null &&
                guestLoginButton != null &&
                registerButton != null &&
                forgotPasswordButton != null &&
                messageText != null &&
                loginPanel != null &&
                registerPanel != null &&
                forgotPasswordPanel != null;

            if (!valid)
            {
                Debug.LogError("LoginUI is missing scene-bound UI references.", this);
            }

            return valid;
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            loginButton.onClick.AddListener(OnLoginButtonClick);
            guestLoginButton.onClick.AddListener(OnGuestLoginButtonClick);
            registerButton.onClick.AddListener(OnRegisterButtonClick);
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordButtonClick);
            buttonsBound = true;

            Utils.Logger.Log("Login UI buttons bound");
        }

        private void InitializeRememberMe()
        {
            if (rememberMeToggle == null)
            {
                return;
            }

            rememberMeToggle.isOn = false;
            rememberMeToggle.onValueChanged.AddListener(OnRememberMeToggleChanged);
            UpdateCheckmarkVisibility(rememberMeToggle.isOn);
        }

        private void DisablePanelBackgroundRaycasts()
        {
            DisableBackgroundRaycast(loginPanel);
            DisableBackgroundRaycast(registerPanel);
            DisableBackgroundRaycast(forgotPasswordPanel);
        }

        private static void DisableBackgroundRaycast(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            Image background = panel.GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = false;
            }
        }

        private IEnumerator TryAutoLogin()
        {
            while (GameManager.Instance != null &&
                   GameManager.Instance.CurrentState != GameManager.GameState.Login &&
                   GameManager.Instance.CurrentState != GameManager.GameState.InGame)
            {
                yield return null;
            }

            yield return Login.LoginManager.Instance.TryAutoLogin(success =>
            {
                if (success)
                {
                    ShowMessage("Auto login successful", Color.green);
                    Login.LoginManager.Instance.EnterGame();
                }
                else
                {
                    Utils.Logger.Log("No saved login, showing login screen");
                }
            });
        }

        private void LoadRememberedCredentials()
        {
            if (rememberMeToggle == null)
            {
                return;
            }

            string savedUsername = Login.TokenManager.Instance.GetRememberedUsername();
            if (string.IsNullOrEmpty(savedUsername))
            {
                return;
            }

            usernameInput.text = savedUsername;
            rememberMeToggle.isOn = true;
        }

        private void OnLoginButtonClick()
        {
            Utils.Logger.Log("Login button clicked");

            string username = usernameInput.text.Trim();
            string password = passwordInput.text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowMessage("Please enter username and password", Color.red);
                return;
            }

            loginButton.interactable = false;
            ShowMessage("Logging in...", Color.yellow);

            Login.LoginManager.Instance.AccountLogin(username, password, rememberMeToggle != null && rememberMeToggle.isOn, (success, message) =>
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
            Utils.Logger.Log("Guest login button clicked");

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
            Utils.Logger.Log("Register button clicked");
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
        }

        private void OnForgotPasswordButtonClick()
        {
            Utils.Logger.Log("Forgot password button clicked");
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
            if (messageText == null)
            {
                return;
            }

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

        private static void ReleaseCursorForUi()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
