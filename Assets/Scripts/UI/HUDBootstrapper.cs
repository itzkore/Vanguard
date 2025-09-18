using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    [DisallowMultipleComponent]
    public class HUDBootstrapper : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private int fontSize = 36;

        private bool _initialized;

        private void Start()
        {
            if (runOnStart)
            {
                EnsureHud();
            }
        }

        [ContextMenu("Ensure HUD")]
        public void EnsureHud()
        {
            if (_initialized && Application.isPlaying)
            {
                return;
            }

            EnsureEventSystem();

            var existingCanvas = FindObjectOfType<HUDController>()?.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                RefreshExistingHud(existingCanvas);
                _initialized = true;
                return;
            }

            CreateHud();
            _initialized = true;
        }

        private void RefreshExistingHud(Canvas canvas)
        {
            var hud = canvas.GetComponent<HUDController>();
            if (hud != null)
            {
                // We'll recreate labels and reconfigure: remove base and top-left energy
                var baseText = canvas.transform.Find("BaseText");
                if (baseText != null)
                {
                    DestroyImmediate(baseText.gameObject);
                }
                var energyTop = canvas.transform.Find("EnergyText");
                if (energyTop != null)
                {
                    DestroyImmediate(energyTop.gameObject);
                }
            }

            RemoveChildrenWith<BuildMenuController>(canvas.transform);
            RemoveChildrenWith<RunControlPanel>(canvas.transform);

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var energyLabel = CreateBuildMenu(canvas.transform, font);

            // Ensure or create Wave label (top center)
            Text waveLabel = null;
            var waveNode = canvas.transform.Find("WaveText");
            if (waveNode != null)
            {
                waveLabel = waveNode.GetComponent<Text>();
                if (waveLabel == null)
                {
                    DestroyImmediate(waveNode.gameObject);
                }
            }
            if (waveLabel == null)
            {
                waveLabel = CreateLabel(canvas.transform, "WaveText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), font, "Wave 0", TextAnchor.MiddleCenter);
            }

            var hudCtl = canvas.GetComponent<HUDController>();
            if (hudCtl == null)
            {
                hudCtl = canvas.gameObject.AddComponent<HUDController>();
            }
            hudCtl.Configure(null, energyLabel, waveLabel);

            CreateRunControls(canvas.transform, font);
        }

        private void CreateHud()
        {
            var canvasGO = new GameObject("HUDCanvas", typeof(RectTransform));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            // Energy label is now created inside the BuildMenu panel (near the build bar)
            var energyLabel = CreateBuildMenu(canvas.transform, font);
            var waveLabel = CreateLabel(canvas.transform, "WaveText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), font, "Wave 0", TextAnchor.MiddleCenter);

            var hud = canvasGO.AddComponent<HUDController>();
            hud.Configure(null, energyLabel, waveLabel);
            CreateRunControls(canvas.transform, font);
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private Text CreateLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Font font, string defaultText, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(360f, 48f);

            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = defaultText;
            label.raycastTarget = false;

            return label;
        }

        private Text CreateBuildMenu(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<BuildMenuController>(canvasRoot);

            var panelGO = new GameObject("BuildMenu", typeof(RectTransform));
            panelGO.layer = LayerMask.NameToLayer("UI");
            var rect = panelGO.GetComponent<RectTransform>();
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(32f, 32f);
            rect.sizeDelta = new Vector2(260f, 10f);

            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.07f, 0.09f, 0.13f, 0.85f);

            var layout = panelGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add Energy label at the top of the build menu panel
            var energyLabel = CreateLabel(panelGO.transform, "EnergyText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), font, "Energy: 0", TextAnchor.MiddleLeft);
            var energyRect = energyLabel.GetComponent<RectTransform>();
            energyRect.sizeDelta = new Vector2(0f, 36f);

            var menu = panelGO.AddComponent<BuildMenuController>();
            menu.Refresh();

            return energyLabel;
        }

        private void CreateRunControls(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<RunControlPanel>(canvasRoot);

            var panelGO = new GameObject("RunControls", typeof(RectTransform));
            panelGO.layer = LayerMask.NameToLayer("UI");
            var rect = panelGO.GetComponent<RectTransform>();
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 32f);
            rect.sizeDelta = new Vector2(200f, 60f);

            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.07f, 0.09f, 0.13f, 0.85f);

            var layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var startButton = CreateButton(panelGO.transform, "Start", font);
            startButton.onClick.AddListener(OnStartButtonClicked);

            var controls = panelGO.AddComponent<RunControlPanel>();
            controls.Configure(startButton);
        }

        private Button CreateButton(Transform parent, string text, Font font)
        {
            var buttonGO = new GameObject($"Button_{text}", typeof(RectTransform));
            buttonGO.layer = LayerMask.NameToLayer("UI");
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(120f, 48f);

            var layoutElement = buttonGO.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 120f;
            layoutElement.minWidth = 100f;
            layoutElement.preferredHeight = 48f;
            layoutElement.minHeight = 40f;

            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.32f, 0.9f);
            image.raycastTarget = true;

            var button = buttonGO.AddComponent<Button>();

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGO.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = font;
            label.fontSize = fontSize;

            return button;
        }

        private void OnStartButtonClicked()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StartRun();
            }

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StartSequence();
            }
        }

        private void EnsureRunControls(Transform hudRoot)
        {
            if (hudRoot == null)
            {
                return;
            }

            RemoveChildrenWith<RunControlPanel>(hudRoot);

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            CreateRunControls(hudRoot, font);
        }

        private void RemoveChildrenWith<T>(Transform root) where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                var go = component.gameObject;
                if (Application.isPlaying)
                {
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }
        }
    }
}
