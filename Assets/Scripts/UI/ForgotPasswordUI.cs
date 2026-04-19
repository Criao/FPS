using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FPSGame.Utils;

namespace FPSGame.UI
{
    /// <summary>
    /// 找回密码界面UI控制器
    /// </summary>
    public class ForgotPasswordUI : MonoBehaviour
    {
        [Header("UI引用")]
        public TMP_InputField emailInput;
        public Button submitButton;
        public Button backButton;
        public TextMeshProUGUI messageText;

        private LoginUI loginUI;

        private void Start()
        {
            loginUI = FindObjectOfType<LoginUI>();

            submitButton.onClick.AddListener(OnSubmitButtonClick);
            backButton.onClick.AddListener(OnBackButtonClick);
        }

        private void OnSubmitButtonClick()
        {
            string email = emailInput.text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                ShowMessage("Please enter email address", Color.red);
                return;
            }

            if (!email.Contains("@"))
            {
                ShowMessage("Invalid email format", Color.red);
                return;
            }

            submitButton.interactable = false;
            ShowMessage("Sending...", Color.yellow);

            Login.LoginManager.Instance.ForgotPassword(email, (success, message) =>
            {
                submitButton.interactable = true;

                if (success)
                {
                    ShowMessage(message, Color.green);
                    Invoke("OnBackButtonClick", 2f);
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
                // Fallback: just disable forgot password panel
                gameObject.SetActive(false);
            }
        }

        private void ShowMessage(string message, Color color)
        {
            messageText.text = message;
            messageText.color = color;
            Utils.Logger.Log($"Forgot Password UI Message: {message}");
        }
    }
}
