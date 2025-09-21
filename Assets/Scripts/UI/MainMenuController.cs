using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Wiring (auto if empty)")] 
        [SerializeField] private Canvas canvas;
        [SerializeField] private Button startButton;
        [SerializeField] private Image background;

        [Header("Style")] 
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.11f, 0.13f, 1f); // Anthracite
        [SerializeField] private bool useNoiseBackground = true;
        [SerializeField] private float fadeInDuration = 0.65f;
    [SerializeField, Range(0f,1f)] private float overlayAlpha = 0.35f;

    private CanvasGroup _canvasGroup;
    private bool _built;
    private bool _styled; // ensures we only style once per enable unless forced

        private void Awake()
        {
            EnsureUI();
        }

        private void OnEnable()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStart);
            }
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged += OnStateChanged;
                OnStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStart);
            }
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged -= OnStateChanged;
            }
        }

        private void OnStart()
        {
            GameManager.Instance?.StartRun();
            gameObject.SetActive(false);
        }

        private void OnStateChanged(GameManager.GameState state)
        {
            bool shouldShow = state == GameManager.GameState.MainMenu;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(shouldShow);
                if (shouldShow && _canvasGroup != null)
                {
                    // Restart fade when returning to menu
                    _canvasGroup.alpha = 0f;
                    StopAllCoroutines();
                    StartCoroutine(FadeInRoutine());
                }
            }
            else
            {
                gameObject.SetActive(shouldShow);
            }
        }

        private void EnsureUI()
        {
            if (_built)
            {
                if (!_styled) ApplyStyling();
                return;
            }
            if (canvas != null && startButton != null)
            {
                // Existing layout in scene (probably prefab) -> just restyle it.
                _canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                _canvasGroup.alpha = 0f;
                ApplyStyling();
                StartCoroutine(FadeInRoutine());
                _built = true;
                return;
            }

            var rootGO = new GameObject("MainMenuCanvas", typeof(RectTransform));
            rootGO.layer = LayerMask.NameToLayer("UI");
            rootGO.transform.SetParent(transform, false);
            canvas = rootGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = rootGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            rootGO.AddComponent<GraphicRaycaster>();
            canvas.sortingOrder = 1000; // ensure above HUD

            var font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();

            // Background covering the whole screen
            var bgGO = new GameObject("Background", typeof(RectTransform));
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.SetParent(rootGO.transform, false);
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            background = bgGO.AddComponent<Image>();
            // Try user supplied sprite first
            var tex = Resources.Load<Sprite>("Art/bgmenu");
            if (tex != null)
            {
                background.sprite = tex;
                background.color = Color.white;
            }
            else
            {
                background.color = backgroundColor;
            }
            background.raycastTarget = true; // block clicks to the game field
            // Remove any existing flash FX if present (avoid startup flash)
            var oldFx = bgGO.GetComponent<MainMenuBackgroundFX>();
            if (oldFx != null) Destroy(oldFx);

            // Optional noise overlay child (separate RawImage so we retain flat color base)
            GameObject noiseGO = null;
            if (useNoiseBackground)
            {
                noiseGO = new GameObject("NoiseOverlay", typeof(RectTransform));
                var nrt = noiseGO.GetComponent<RectTransform>();
                nrt.SetParent(bgGO.transform, false);
                nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
                var raw = noiseGO.AddComponent<UnityEngine.UI.RawImage>();
                raw.color = new Color(1f,1f,1f, overlayAlpha);
                var gen = noiseGO.AddComponent<TexturedBackgroundGenerator>();
                gen.ApplyImmediately();
            }

            // Fade group
            _canvasGroup = rootGO.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform));
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.SetParent(rootGO.transform, false);
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -120);
            titleRT.sizeDelta = new Vector2(1000, 140);
            var titleText = titleGO.AddComponent<Text>();
            titleText.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get(bold: true); // Use bold Orbitron for the logo
            titleText.fontSize = 84; // Increased size to showcase the Orbitron font
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "VANGUARD";
            titleText.color = new Color(0.95f, 0.96f, 1f, 1f);
            // Add subtle shadow/outline for a classy look
            var titleShadow = titleGO.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            titleShadow.effectDistance = new Vector2(2f, -2f);
            var titleOutline = titleGO.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.15f, 0.18f, 0.22f, 0.5f);
            titleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Start button (dark sci‑fi look)
            var btnGO = CreateRetroButton(rootGO.transform, font, "START", new Vector2(0.5f, 0.5f), new Vector2(0, -40));
            startButton = btnGO.GetComponent<Button>();
            if (startButton != null && startButton.GetComponent<AnimatedButton>() == null)
            {
                startButton.gameObject.AddComponent<AnimatedButton>();
            }

            StartCoroutine(FadeInRoutine());
            _built = true;
            _styled = true;
        }

        private GameObject CreateRetroButton(Transform parent, Font font, string label, Vector2 anchor, Vector2 offset)
        {
            // Outer frame
            var outer = new GameObject(label + "_Button", typeof(RectTransform));
            var ort = outer.GetComponent<RectTransform>();
            ort.SetParent(parent, false);
            ort.anchorMin = anchor; ort.anchorMax = anchor; ort.pivot = new Vector2(0.5f, 0.5f);
            ort.anchoredPosition = offset; ort.sizeDelta = new Vector2(320, 90);

            var outerImg = outer.AddComponent<Image>();
            outerImg.color = new Color(0.18f, 0.20f, 0.24f, 1f); // dark frame
            var outerOutline = outer.AddComponent<Outline>();
            outerOutline.effectColor = new Color(0.05f, 0.06f, 0.07f, 1f);
            outerOutline.effectDistance = new Vector2(2f, -2f);

            // Inner plate
            var inner = new GameObject("Inner", typeof(RectTransform));
            var irt = inner.GetComponent<RectTransform>();
            irt.SetParent(outer.transform, false);
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(10, 10); irt.offsetMax = new Vector2(-10, -10);

            var innerImg = inner.AddComponent<Image>();
            innerImg.color = new Color(0.28f, 0.30f, 0.35f, 1f); // inner panel
            var innerShadow = inner.AddComponent<Shadow>();
            innerShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            innerShadow.effectDistance = new Vector2(2f, -2f);

            // Button component on inner plate
            var button = inner.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = innerImg.color;
            colors.highlightedColor = new Color(0.34f, 0.37f, 0.43f, 1f);
            colors.pressedColor = new Color(0.22f, 0.24f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;

            var textGO = new GameObject("Text", typeof(RectTransform));
            var trt = textGO.GetComponent<RectTransform>();
            trt.SetParent(inner.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.font = font; text.text = label; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(0.88f, 0.91f, 0.97f, 1f); text.fontSize = 38;
            var txtShadow = textGO.AddComponent<Shadow>();
            txtShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            txtShadow.effectDistance = new Vector2(2f, -2f);

            return inner; // return the clickable element with Button component
        }

        private System.Collections.IEnumerator FadeInRoutine()
        {
            if (_canvasGroup == null) yield break;
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / fadeInDuration);
                _canvasGroup.alpha = a;
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        private void ApplyStyling()
        {
            if (_styled) return;

            // Background restyle (even if it already existed)
            if (background == null && canvas != null)
            {
                var existingBg = canvas.transform.Find("Background");
                if (existingBg != null) background = existingBg.GetComponent<Image>();
            }
            if (background != null)
            {
                if (background.sprite == null || ApproximatelyOldSilver(background.color))
                    background.color = backgroundColor;
                var oldFx = background.GetComponent<MainMenuBackgroundFX>();
                if (oldFx != null) DestroyImmediate(oldFx);

                // Ensure overlay child exists / updated
                var existingOverlay = background.transform.Find("NoiseOverlay");
                if (useNoiseBackground)
                {
                    if (existingOverlay == null)
                    {
                        var noiseGO = new GameObject("NoiseOverlay", typeof(RectTransform));
                        var nrt = noiseGO.GetComponent<RectTransform>();
                        nrt.SetParent(background.transform, false);
                        nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
                        var raw = noiseGO.AddComponent<UnityEngine.UI.RawImage>();
                        raw.color = new Color(1f,1f,1f, overlayAlpha);
                        var gen = noiseGO.AddComponent<TexturedBackgroundGenerator>();
                        gen.ApplyImmediately();
                    }
                    else
                    {
                        var raw = existingOverlay.GetComponent<UnityEngine.UI.RawImage>();
                        if (raw != null) raw.color = new Color(1f,1f,1f, overlayAlpha);
                    }
                }
                else if (existingOverlay != null)
                {
                    existingOverlay.gameObject.SetActive(false);
                }
            }

            // Button restyle
            if (startButton == null && canvas != null)
            {
                // Try to find a START button
                var btn = canvas.GetComponentInChildren<Button>(true);
                if (btn != null && btn.name.ToLower().Contains("start")) startButton = btn;
            }
            if (startButton != null)
            {
                if (startButton.GetComponent<AnimatedButton>() == null)
                    startButton.gameObject.AddComponent<AnimatedButton>();

                // Inner image is on same GameObject we returned from CreateRetroButton (Button component host)
                var innerImg = startButton.GetComponent<Image>();
                if (innerImg != null && ApproximatelyOldButtonSilver(innerImg.color))
                {
                    innerImg.color = new Color(0.28f, 0.30f, 0.35f, 1f);
                    var colors = startButton.colors;
                    colors.normalColor = innerImg.color;
                    colors.highlightedColor = new Color(0.34f, 0.37f, 0.43f, 1f);
                    colors.pressedColor = new Color(0.22f, 0.24f, 0.28f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    startButton.colors = colors;
                }

                // Text child
                var txt = startButton.GetComponentInChildren<Text>(true);
                if (txt != null && txt.color.r < 0.5f) // old dark text -> make light
                {
                    txt.color = new Color(0.88f, 0.91f, 0.97f, 1f);
                }
            }

            _styled = true;
        }

        private bool ApproximatelyOldSilver(Color c)
        {
            // Old fallback ~0.78 grey
            return Mathf.Abs(c.r - 0.78f) < 0.06f && Mathf.Abs(c.g - 0.78f) < 0.06f && Mathf.Abs(c.b - 0.82f) < 0.08f;
        }
        private bool ApproximatelyOldButtonSilver(Color c)
        {
            // Old inner button ~0.82 grey
            return c.r > 0.7f && c.g > 0.7f && c.b > 0.75f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                // Allow instant preview when tweaking in Inspector
                ApplyStyling();
            }
        }
#endif
    }
}
