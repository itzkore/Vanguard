using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statsText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [Header("Debug")]
        [SerializeField, Tooltip("Enable verbose logging for GameOver UI lifecycle and button clicks.")] private bool debugLogging = false;

        private void Awake()
        {
            EnsureUI();
            Hide();
            if (debugLogging) Debug.Log("[GameOverController] Awake completed, UI ensured & hidden.");
        }

        private void OnEnable()
        {
            if (debugLogging) Debug.Log("[GameOverController] OnEnable");
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged += OnStateChanged;
            }
            WireButtonListeners();
        }

        private void OnDisable()
        {
            if (debugLogging) Debug.Log("[GameOverController] OnDisable");
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged -= OnStateChanged;
            }
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestart);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMainMenu);
        }

        private void OnStateChanged(GameManager.GameState state)
        {
            if (debugLogging) Debug.Log($"[GameOverController] StateChanged -> {state}");
            if (state == GameManager.GameState.GameOver)
            {
                EnsureUI(); // self-heal if something destroyed UI mid-run
                WireButtonListeners();
                ShowWithStats();
            }
            else
            {
                Hide();
            }
        }

        private void ShowWithStats()
        {
            if (debugLogging) Debug.Log("[GameOverController] ShowWithStats");
            int kills = WaveManager.HasInstance ? WaveManager.Instance.TotalKills : 0;
            int wave = WaveManager.HasInstance ? WaveManager.Instance.CurrentWaveNumber : 0;
            if (titleText != null) titleText.text = "Game Over";
            if (statsText != null) statsText.text = $"Kills: {kills}\nWave: {wave}";
            if (canvas != null) canvas.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnRestart()
        {
            if (debugLogging) Debug.Log("[GameOverController] Restart clicked");
            GameManager.Instance?.StartRun();
        }

        private void OnMainMenu()
        {
            if (debugLogging) Debug.Log("[GameOverController] Main Menu clicked");
            GameManager.Instance?.ReturnToMenu();
        }

        private void EnsureUI()
        {
            if (canvas != null && restartButton != null && menuButton != null && titleText != null && statsText != null) return;
            if (debugLogging) Debug.Log("[GameOverController] (Re)building GameOver UI");

            var rootGO = new GameObject("GameOverCanvas", typeof(RectTransform));
            rootGO.layer = LayerMask.NameToLayer("UI");
            rootGO.transform.SetParent(transform, false);
            canvas = rootGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000; // below PauseMenu (5000) but above main HUD
            var scaler = rootGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            rootGO.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var panelGO = new GameObject("Panel", typeof(RectTransform));
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.SetParent(rootGO.transform, false);
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(560, 340);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.82f);

            var titleGO = new GameObject("Title", typeof(RectTransform));
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.SetParent(panelRT, false);
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -28);
            titleRT.sizeDelta = new Vector2(520, 72);
            titleText = titleGO.AddComponent<Text>();
            titleText.font = font; titleText.fontSize = 40; titleText.alignment = TextAnchor.MiddleCenter; titleText.color = Color.white;

            var statsGO = new GameObject("Stats", typeof(RectTransform));
            var statsRT = statsGO.GetComponent<RectTransform>();
            statsRT.SetParent(panelRT, false);
            statsRT.anchorMin = new Vector2(0.5f, 0.5f);
            statsRT.anchorMax = new Vector2(0.5f, 0.5f);
            statsRT.pivot = new Vector2(0.5f, 0.5f);
            statsRT.anchoredPosition = new Vector2(0, 24);
            statsRT.sizeDelta = new Vector2(520, 130);
            statsText = statsGO.AddComponent<Text>();
            statsText.font = font; statsText.fontSize = 26; statsText.alignment = TextAnchor.MiddleCenter; statsText.color = Color.white;

            restartButton = CreateButton(panelRT, font, "Restart", new Vector2(0.5f, 0f), new Vector2(-120, 40)).GetComponent<Button>();
            menuButton = CreateButton(panelRT, font, "Main Menu", new Vector2(0.5f, 0f), new Vector2(120, 40)).GetComponent<Button>();
        }

        private void WireButtonListeners()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestart);
                restartButton.onClick.AddListener(OnRestart);
            }
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMainMenu);
                menuButton.onClick.AddListener(OnMainMenu);
            }
        }

        private GameObject CreateButton(Transform parent, Font font, string label, Vector2 anchor, Vector2 offset)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(180, 48);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            button.colors = colors;

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.SetParent(rt, false);
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one; textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.font = font; text.text = label; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.fontSize = 22;

            return go;
        }
    }
}
