using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSGame.Utils;

namespace FPSGame.UI
{
    /// <summary>
    /// 注册界面UI控制器
    /// </summary>
    public class RegisterUI : MonoBehaviour
    {
        [Header("UI引用")]
        public TMP_InputField usernameInput;
        public TMP_InputField emailInput;
        public TMP_InputField passwordInput;
        public TMP_InputField confirmPasswordInput;
        public Button registerButton;
        public Button backButton;
        public TextMeshProUGUI messageText;

        private LoginUI loginUI;

        private void Start()
        {
            loginUI = FindObjectOfType<LoginUI>();

            registerButton.onClick.AddListener(OnRegisterButtonClick);
            backButton.onClick.AddListener(OnBackButtonClick);
        }

        private void OnRegisterButtonClick()
        {
            string username = usernameInput.text.Trim();
            string email = emailInput.text.Trim();
            string password = passwordInput.text;
            string confirmPassword = confirmPasswordInput.text;

            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowMessage("Please fill all fields", Color.red);
                return;
            }

            if (password != confirmPassword)
            {
                ShowMessage("Passwords do not match", Color.red);
                return;
            }

            if (password.Length < 6)
            {
                ShowMessage("Password must be at least 6 characters", Color.red);
                return;
            }

            if (!email.Contains("@"))
            {
                ShowMessage("Invalid email format", Color.red);
                return;
            }

            registerButton.interactable = false;
            ShowMessage("Registering...", Color.yellow);

            Login.LoginManager.Instance.Register(username, email, password, (success, message) =>
            {
                registerButton.interactable = true;

                if (success)
                {
                    ShowMessage(message, Color.green);
                    Invoke("OnBackButtonClick", 1.5f);
                }
                else
                {
                    ShowMessage(message, Color.red);
                }
            });
        }

        private void OnBackButtonClick()
        {
            if (loginUI != null)
            {
                loginUI.ShowLoginPanel();
            }
            else
            {
                // Fallback: just disable register panel
                gameObject.SetActive(false);
            }
        }

        private void ShowMessage(string message, Color color)
        {
            messageText.text = message;
            messageText.color = color;
            Utils.Logger.Log($"Register UI Message: {message}");
        }
    }
}
