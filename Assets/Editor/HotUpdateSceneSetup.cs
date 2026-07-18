using FPSGame.Core;
using FPSGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FPSGame.EditorTools
{
    [InitializeOnLoad]
    public static class HotUpdateSceneSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Launcher.unity",
            "Assets/Scenes/Login.unity"
        };

        static HotUpdateSceneSetup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("FPS Game/Setup Hot Update UI")]
        public static void ApplyToAllScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string activeScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (string scenePath in ScenePaths)
            {
                EditorSceneManager.OpenScene(scenePath);
                SetupCurrentScene();
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"Hot update UI setup complete: {scenePath}");
            }

            if (!Application.isBatchMode && !string.IsNullOrEmpty(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath);
            }
        }

        [MenuItem("FPS Game/Setup Hot Update UI In Current Scene")]
        public static void ApplyToCurrentScene()
        {
            SetupCurrentScene();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Hot update UI setup complete: {EditorSceneManager.GetActiveScene().path}");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (Object.FindObjectOfType<GameManager>(true) == null ||
                Object.FindObjectOfType<HotUpdateUI>(true) != null)
            {
                return;
            }

            SetupCurrentScene();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Hot update UI was missing and has been added to the current scene before Play.");
        }

        private static void SetupCurrentScene()
        {
            HotUpdateUI hotUpdateUI = Object.FindObjectOfType<HotUpdateUI>(true);
            GameObject root = hotUpdateUI != null
                ? hotUpdateUI.gameObject
                : new GameObject("HotUpdateUI", typeof(RectTransform));

            root.name = "HotUpdateUI";
            root.SetActive(true);
            root.layer = GetUiLayer();
            EnsureComponent<RectTransform>(root);

            hotUpdateUI = root.GetComponent<HotUpdateUI>();
            if (hotUpdateUI == null)
            {
                hotUpdateUI = root.AddComponent<HotUpdateUI>();
            }

            ClearChildren(root.transform);

            Canvas canvas = EnsureComponent<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureComponent<GraphicRaycaster>(root);

            GameObject overlay = CreateRect("Overlay", root.transform);
            Stretch(overlay.GetComponent<RectTransform>());
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0.02f, 0.025f, 0.03f, 0.82f);

            GameObject panel = CreateRect("Update Panel", overlay.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            SetRect(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 390f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.1f, 0.96f);

            TextMeshProUGUI titleText = CreateText("Title", panel.transform, "Checking Updates", 44, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(680f, 62f));

            TextMeshProUGUI statusText = CreateText("Status", panel.transform, "Checking version and patch files...", 28, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(680f, 46f));

            TextMeshProUGUI detailText = CreateText("Detail", panel.transform, "Current version: unknown", 22, FontStyles.Normal, TextAlignmentOptions.Center);
            detailText.enableWordWrapping = true;
            SetRect(detailText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -188f), new Vector2(680f, 86f));

            Slider progressSlider = CreateProgressSlider(panel.transform, out TextMeshProUGUI progressText);
            Button retryButton = CreateButton("Retry Button", "Retry", panel.transform, new Vector2(-190f, 38f));
            Button continueButton = CreateButton("Continue Button", "Enter Game", panel.transform, new Vector2(0f, 38f));
            Button exitButton = CreateButton("Exit Button", "Exit", panel.transform, new Vector2(190f, 38f));

            overlay.SetActive(false);

            SerializedObject serializedObject = new SerializedObject(hotUpdateUI);
            serializedObject.FindProperty("panelRoot").objectReferenceValue = overlay;
            serializedObject.FindProperty("canvas").objectReferenceValue = canvas;
            serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
            serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
            serializedObject.FindProperty("detailText").objectReferenceValue = detailText;
            serializedObject.FindProperty("progressText").objectReferenceValue = progressText;
            serializedObject.FindProperty("progressSlider").objectReferenceValue = progressSlider;
            serializedObject.FindProperty("retryButton").objectReferenceValue = retryButton;
            serializedObject.FindProperty("continueButton").objectReferenceValue = continueButton;
            serializedObject.FindProperty("exitButton").objectReferenceValue = exitButton;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursive(root, GetUiLayer());
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static Slider CreateProgressSlider(Transform parent, out TextMeshProUGUI progressText)
        {
            GameObject sliderObject = CreateRect("Progress", parent);
            SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-32f, 96f), new Vector2(560f, 28f));

            Image background = sliderObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.22f, 0.24f, 1f);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            slider.targetGraphic = background;

            GameObject fillArea = CreateRect("Fill Area", sliderObject.transform);
            Stretch(fillArea.GetComponent<RectTransform>());

            GameObject fill = CreateRect("Fill", fillArea.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);

            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.15f, 0.72f, 0.95f, 1f);
            slider.fillRect = fillRect;

            progressText = CreateText("Progress Text", parent, "0%", 20, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(progressText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(288f, 96f), new Vector2(96f, 32f));

            return slider;
        }

        private static Button CreateButton(string name, string label, Transform parent, Vector2 position)
        {
            GameObject buttonObject = CreateRect(name, parent);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(170f, 48f));

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.92f, 0.94f, 0.96f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = new Color(0.08f, 0.09f, 0.1f, 1f);
            Stretch(text.rectTransform);

            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value, int fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateRect(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        private static int GetUiLayer()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? uiLayer : 0;
        }
    }
}
