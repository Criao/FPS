using FPSGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPSGame.EditorTools
{
    public static class LoginSceneSetup
    {
        private const string LoginScenePath = "Assets/Scenes/Login.unity";

        [MenuItem("FPS Game/Setup Login Scene")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(LoginScenePath);

            ForgotPasswordUI forgotPasswordUI = Object.FindObjectOfType<ForgotPasswordUI>(true);
            if (forgotPasswordUI == null)
            {
                Debug.LogError("ForgotPasswordUI was not found in Login scene.");
                return;
            }

            TMP_InputField emailInput = forgotPasswordUI.emailInput;
            if (emailInput == null)
            {
                Debug.LogError("ForgotPasswordUI.emailInput must be assigned before setup.");
                return;
            }

            RectTransform emailRect = emailInput.GetComponent<RectTransform>();
            emailRect.anchoredPosition = new Vector2(0f, 105f);
            emailRect.sizeDelta = new Vector2(320f, 54f);

            forgotPasswordUI.resetCodeInput = CreateOrUpdateInput(
                emailInput,
                "ResetCodeInput",
                "Reset Code",
                new Vector2(0f, 38f),
                TMP_InputField.ContentType.IntegerNumber);

            forgotPasswordUI.newPasswordInput = CreateOrUpdateInput(
                emailInput,
                "NewPasswordInput",
                "New Password",
                new Vector2(0f, -29f),
                TMP_InputField.ContentType.Password);

            forgotPasswordUI.confirmPasswordInput = CreateOrUpdateInput(
                emailInput,
                "ConfirmPasswordInput",
                "Confirm Password",
                new Vector2(0f, -96f),
                TMP_InputField.ContentType.Password);

            PositionButton(forgotPasswordUI.submitButton, new Vector2(0f, -178f), new Vector2(180f, 48f));
            PositionButton(forgotPasswordUI.backButton, new Vector2(0f, -244f), new Vector2(160f, 42f));
            PositionText(forgotPasswordUI.messageText, new Vector2(0f, -305f), new Vector2(520f, 48f));
            SetButtonLabel(forgotPasswordUI.submitButton, "Send Code");

            EditorUtility.SetDirty(forgotPasswordUI);
            EditorSceneManager.MarkSceneDirty(forgotPasswordUI.gameObject.scene);
            EditorSceneManager.SaveScene(forgotPasswordUI.gameObject.scene);
            Debug.Log("Login scene forgot-password UI setup complete.");
        }

        private static TMP_InputField CreateOrUpdateInput(
            TMP_InputField source,
            string objectName,
            string placeholder,
            Vector2 anchoredPosition,
            TMP_InputField.ContentType contentType)
        {
            Transform parent = source.transform.parent;
            Transform existing = parent.Find(objectName);
            TMP_InputField input = existing != null
                ? existing.GetComponent<TMP_InputField>()
                : Object.Instantiate(source, parent);

            input.name = objectName;
            input.text = string.Empty;
            input.contentType = contentType;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = contentType == TMP_InputField.ContentType.IntegerNumber ? 6 : 0;
            input.gameObject.SetActive(false);

            RectTransform inputRect = input.GetComponent<RectTransform>();
            inputRect.anchoredPosition = anchoredPosition;
            inputRect.sizeDelta = new Vector2(320f, 54f);

            if (input.placeholder is TextMeshProUGUI placeholderText)
            {
                placeholderText.text = placeholder;
            }

            if (input.textComponent != null)
            {
                input.textComponent.text = string.Empty;
            }

            EditorUtility.SetDirty(input);
            EditorUtility.SetDirty(input.gameObject);
            return input;
        }

        private static void PositionButton(UnityEngine.UI.Button button, Vector2 anchoredPosition, Vector2 size)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(button);
        }

        private static void PositionText(TextMeshProUGUI text, Vector2 anchoredPosition, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            text.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(text);
        }

        private static void SetButtonLabel(UnityEngine.UI.Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonText != null)
            {
                buttonText.text = label;
                EditorUtility.SetDirty(buttonText);
            }
        }
    }
}
