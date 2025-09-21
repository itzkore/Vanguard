using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class MainMenuController : MonoBehaviour
    {
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button startButton;
    [SerializeField] private Image background;

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
            }
            else
            {
                gameObject.SetActive(shouldShow);
            }
        }

        private void EnsureUI()
        {
            if (canvas != null && startButton != null) return;

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

            // Silver background covering the whole screen
            var bgGO = new GameObject("Background", typeof(RectTransform));
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.SetParent(rootGO.transform, false);
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            background = bgGO.AddComponent<Image>();
            // If a user imported bgmenu.png, try to auto-load it (Assets/Art/bgmenu.png). Put it under Resources/Art for auto Resources.Load.
            var tex = Resources.Load<Sprite>("Art/bgmenu");
            if (tex != null)
            {
                background.sprite = tex;
                background.color = Color.white;
            }
            else
            {
                background.color = new Color(0.78f, 0.78f, 0.82f, 1f); // soft fallback
            }
            background.raycastTarget = true; // block clicks to the game field
            // Add FX component (creates smoke / flashes / glitch overlays)
            if (bgGO.GetComponent<MainMenuBackgroundFX>() == null)
            {
                var fx = bgGO.AddComponent<MainMenuBackgroundFX>();
                // Leave default serialized settings; user can tweak in inspector.
            }

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

            // Start button (retro analog look)
            var btnGO = CreateRetroButton(rootGO.transform, font, "START", new Vector2(0.5f, 0.5f), new Vector2(0, -40));
            startButton = btnGO.GetComponent<Button>();
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
            outerImg.color = new Color(0.55f, 0.57f, 0.60f, 1f); // brushed metal frame
            var outerOutline = outer.AddComponent<Outline>();
            outerOutline.effectColor = new Color(0.3f, 0.33f, 0.36f, 1f);
            outerOutline.effectDistance = new Vector2(2f, -2f);

            // Inner plate
            var inner = new GameObject("Inner", typeof(RectTransform));
            var irt = inner.GetComponent<RectTransform>();
            irt.SetParent(outer.transform, false);
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0.5f, 0.5f);
            irt.offsetMin = new Vector2(10, 10); irt.offsetMax = new Vector2(-10, -10);

            var innerImg = inner.AddComponent<Image>();
            innerImg.color = new Color(0.82f, 0.83f, 0.86f, 1f); // lighter silver
            var innerShadow = inner.AddComponent<Shadow>();
            innerShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            innerShadow.effectDistance = new Vector2(2f, -2f);

            // Button component on inner plate
            var button = inner.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = innerImg.color;
            colors.highlightedColor = new Color(0.88f, 0.89f, 0.92f, 1f);
            colors.pressedColor = new Color(0.74f, 0.75f, 0.78f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;

            var textGO = new GameObject("Text", typeof(RectTransform));
            var trt = textGO.GetComponent<RectTransform>();
            trt.SetParent(inner.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.font = font; text.text = label; text.alignment = TextAnchor.MiddleCenter; text.color = new Color(0.1f, 0.12f, 0.15f, 1f); text.fontSize = 36;
            var txtShadow = textGO.AddComponent<Shadow>();
            txtShadow.effectColor = new Color(1f, 1f, 1f, 0.25f);
            txtShadow.effectDistance = new Vector2(-1f, 1f);

            return inner; // return the clickable element with Button component
        }
    }
}
