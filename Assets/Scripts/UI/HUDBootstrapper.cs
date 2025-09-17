using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

            var existingHud = FindObjectOfType<HUDController>();
            if (existingHud != null)
            {
                if (Application.isPlaying)
                {
                    existingHud.Configure(existingHud.BaseHealthText, existingHud.EnergyText, existingHud.WaveText);
                }
                _initialized = true;
                return;
            }

            EnsureEventSystem();

            var canvasGO = new GameObject("HUDCanvas", typeof(RectTransform));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            canvasGO.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var baseLabel = CreateLabel(canvas.transform, "BaseText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), font, "Base: 0/0", TextAnchor.MiddleLeft);
            var energyLabel = CreateLabel(canvas.transform, "EnergyText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -84f), font, "Energy: 0", TextAnchor.MiddleLeft);
            var waveLabel = CreateLabel(canvas.transform, "WaveText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), font, "Wave 0", TextAnchor.MiddleCenter);

            var hud = canvasGO.AddComponent<HUDController>();
            hud.Configure(baseLabel, energyLabel, waveLabel);

            CreateBuildMenu(canvas.transform, font);

            _initialized = true;
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

        private void CreateBuildMenu(Transform canvasRoot, Font font)
        {
            if (FindObjectOfType<BuildMenuController>() != null)
            {
                return;
            }

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

            var menu = panelGO.AddComponent<BuildMenuController>();
            menu.Refresh();
        }
    }
}
