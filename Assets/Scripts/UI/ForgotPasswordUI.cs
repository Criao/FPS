using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FPSGame.UI
{
    /// <summary>
    /// Controls the forgot-password and reset-password panel.
    /// </summary>
    public class ForgotPasswordUI : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField emailInput;
        public TMP_InputField resetCodeInput;
        public TMP_InputField newPasswordInput;
        public TMP_InputField confirmPasswordInput;
        public Button submitButton;
        public Button backButton;
        public TextMeshProUGUI messageText;

        private const string SendCodeLabel = "Send Code";
        private const string ResetPasswordLabel = "Reset Password";

        private LoginUI loginUI;
        private bool resetMode;

        private void Start()
        {
            loginUI = FindObjectOfType<LoginUI>();

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            resetMode = false;
            ClearResetFields();
            SetResetInputsVisible(false);
            SetSubmitButtonLabel(SendCodeLabel);

            submitButton.onClick.AddListener(OnSubmitButtonClick);
            backButton.onClick.AddListener(OnBackButtonClick);
        }

        private bool ValidateReferences()
        {
            bool valid = emailInput != null &&
                         resetCodeInput != null &&
                         newPasswordInput != null &&
                         confirmPasswordInput != null &&
                         submitButton != null &&
                         backButton != null &&
                         messageText != null;

            if (!valid)
            {
                Debug.LogError("ForgotPasswordUI is missing scene-bound UI references.");
            }

            return valid;
        }

        private void OnSubmitButtonClick()
        {
            if (resetMode)
            {
                SubmitPasswordReset();
            }
            else
            {
                RequestResetCode();
            }
        }

        private void RequestResetCode()
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
                    resetMode = true;
                    SetResetInputsVisible(true);
                    SetSubmitButtonLabel(ResetPasswordLabel);
                }
                else
                {
                    ShowMessage(message, Color.red);
                }
            });
        }

        private void SubmitPasswordReset()
        {
            string email = emailInput.text.Trim();
            string resetCode = resetCodeInput.text.Trim();
            string newPassword = newPasswordInput.text;
            string confirmPassword = confirmPasswordInput.text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(resetCode) || string.IsNullOrEmpty(newPassword))
            {
                ShowMessage("Please fill all fields", Color.red);
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowMessage("Passwords do not match", Color.red);
                return;
            }

            if (newPassword.Length < 6)
            {
                ShowMessage("Password must be at least 6 characters", Color.red);
                return;
            }

            submitButton.interactable = false;
            ShowMessage("Resetting password...", Color.yellow);

            Login.LoginManager.Instance.ResetPassword(email, resetCode, newPassword, (success, message) =>
            {
                submitButton.interactable = true;

                if (success)
                {
                    ShowMessage(message, Color.green);
                    resetMode = false;
                    Invoke(nameof(OnBackButtonClick), 1.5f);
                }
                else
                {
                    ShowMessage(message, Color.red);
                }
            });
        }

        private void OnBackButtonClick()
        {
            resetMode = false;
            ClearResetFields();
            SetResetInputsVisible(false);
            SetSubmitButtonLabel(SendCodeLabel);

            if (loginUI != null)
            {
                loginUI.ShowLoginPanel();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ClearResetFields()
        {
            resetCodeInput.text = string.Empty;
            newPasswordInput.text = string.Empty;
            confirmPasswordInput.text = string.Empty;
        }

        private void SetResetInputsVisible(bool visible)
        {
            resetCodeInput.gameObject.SetActive(visible);
            newPasswordInput.gameObject.SetActive(visible);
            confirmPasswordInput.gameObject.SetActive(visible);
        }

        private void SetSubmitButtonLabel(string label)
        {
            TextMeshProUGUI buttonText = submitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = label;
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
