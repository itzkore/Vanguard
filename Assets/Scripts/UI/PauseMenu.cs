using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // for new Input System keyboard fallback
using BulletHeavenFortressDefense.Managers; // GameManager

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Simple pause menu: press ESC to toggle. Provides Resume, Restart (reload current), Main Menu.
    /// If a Canvas/Buttons are not wired, it will auto-build a basic overlay at runtime.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("References (optional)")]
        [SerializeField] private Canvas rootCanvas; // enables/disables whole menu
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
        [Header("Config")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool pauseTimeScale = true;
        [SerializeField] private bool autoCreateUI = true;
    [Header("Top Right Button (Legacy - HUD now owns creation)")]
    [SerializeField, Tooltip("(Deprecated) Previously created its own top-right Menu button. Left for backwards compatibility; keep FALSE because HUD builds the button now.")] private bool createTopRightButton = false;
    [SerializeField, Tooltip("Label text for the top-right menu button.")] private string topRightButtonText = "Menu";
    [SerializeField, Tooltip("Width of the top-right menu button.")] private float topRightButtonWidth = 120f;
    [SerializeField, Tooltip("Height of the top-right menu button.")] private float topRightButtonHeight = 44f;
    [SerializeField, Tooltip("Screen padding from top-right corner.")] private Vector2 topRightButtonPadding = new Vector2(12f, 12f);
    [SerializeField, Tooltip("Prefer placing the Menu button INSIDE the WallsHealthHUD frame (top-right). If HUD not ready yet it will fall back then reattach.")] private bool preferHUDPlacement = true;
    [SerializeField, Tooltip("Pixels left from HUD frame right edge when embedded.")] private float hudInsetX = 6f;
    [SerializeField, Tooltip("Pixels down from HUD frame top when embedded.")] private float hudInsetY = 6f;
    [SerializeField, Tooltip("Never hide the menu button, even in Main Menu state.")] private bool alwaysShowButton = false;
    [SerializeField, Tooltip("If true, will recreate the top-right button automatically if it gets destroyed.")] private bool autoRecoverTopRightButton = true;
    [SerializeField, Tooltip("How often (seconds) to check for missing top-right button when auto-recovery is enabled.")] private float autoRecoverInterval = 1.0f;

    private Canvas _topRightCanvas;
    private Button _topRightButton;

        private bool _isPaused;
        private float _prePauseTimeScale = 1f;
    [Header("Debug")]
    [SerializeField] private bool logToggleKeyDetection = false;
    [SerializeField, Tooltip("Log creation / embedding lifecycle for the top-right button.")] private bool logButtonLifecycle = true;

        private void Awake()
        {
            if (autoCreateUI)
            {
                EnsureUI();
            }
            WireButtons();
            SetVisible(false, instant:true);
            if (createTopRightButton)
            {
                EnsureTopRightButton();
                if (preferHUDPlacement)
                {
                    StartCoroutine(RetryEmbedRoutine());
                }
            }
        }

        private System.Collections.IEnumerator RetryEmbedRoutine()
        {
            const float timeout = 3f; // seconds
            float endTime = Time.time + timeout;
            while (Time.time < endTime)
            {
                if (TryReparentIntoHUD())
                {
                    if (logButtonLifecycle) Debug.Log("[PauseMenu] Button successfully embedded after retry.");
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            if (logButtonLifecycle) Debug.LogWarning("[PauseMenu] Failed to embed menu button into WallsHealthHUD within timeout.");
        }

        private bool TryReparentIntoHUD()
        {
            if (!preferHUDPlacement || _topRightButton == null) return true;
            var hud = FindObjectOfType<WallsHealthHUD>();
            if (hud == null || hud.FrameRoot == null) return false;
            // If already inside that frame, done
            if (_topRightButton.transform.parent == hud.FrameRoot) return true;
            var rt = _topRightButton.GetComponent<RectTransform>();
            _topRightButton.transform.SetParent(hud.FrameRoot, worldPositionStays:false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-hudInsetX, -hudInsetY);
            if (logButtonLifecycle) Debug.Log("[PauseMenu] Embedded menu button into WallsHealthHUD frame.");
            return true;
        }

        private void Update()
        {
            if (CheckTogglePressed())
            {
                if (_isPaused) Resume(); else Pause();
            }

            // Hide top-right menu button while in Main Menu state unless alwaysShowButton is enabled
            if (_topRightButton != null)
            {
                var gmState = GameManager.HasInstance ? GameManager.Instance.CurrentState : GameManager.GameState.MainMenu;
                bool shouldBeVisible = alwaysShowButton || gmState != GameManager.GameState.MainMenu;
                if (_topRightButton.gameObject.activeSelf != shouldBeVisible)
                {
                    _topRightButton.gameObject.SetActive(shouldBeVisible);
                }
            }
            else if (autoRecoverTopRightButton && createTopRightButton)
            {
                // Throttle recreation attempts
                if (Time.unscaledTime >= _nextRecoverTime)
                {
                    _nextRecoverTime = Time.unscaledTime + autoRecoverInterval;
                    if (logButtonLifecycle) Debug.Log("[PauseMenu] Auto-recover: recreating missing top-right Menu button.");
                    EnsureTopRightButton();
                }
            }
        }

        private float _nextRecoverTime = 0f;

        private void LateUpdate()
        {
            if (!preferHUDPlacement || _topRightButton == null) return;
            var hud = FindObjectOfType<WallsHealthHUD>();
            if (hud == null || hud.FrameRoot == null) return;
            if (_topRightButton.transform.parent != hud.FrameRoot) return; // not embedded yet
            var rt = _topRightButton.GetComponent<RectTransform>();
            // Reassert anchor & offset each frame in case frame size changed.
            rt.anchorMin = new Vector2(1f,1f);
            rt.anchorMax = new Vector2(1f,1f);
            rt.pivot = new Vector2(1f,1f);
            var desired = new Vector2(-hudInsetX, -hudInsetY);
            if ((rt.anchoredPosition - desired).sqrMagnitude > 0.01f)
            {
                rt.anchoredPosition = desired;
            }
        }

        private bool CheckTogglePressed()
        {
            bool pressed = false;
            // 1. Primary assigned key via legacy Input
            if (Input.GetKeyDown(toggleKey)) pressed = true;
            // 2. Always listen for Escape explicitly
            if (!pressed && Input.GetKeyDown(KeyCode.Escape)) pressed = true;
            // 3. New Input System keyboard (supports situations where legacy is disabled or focus issues)
            if (!pressed && Keyboard.current != null)
            {
                if (toggleKey == KeyCode.Escape && Keyboard.current.escapeKey.wasPressedThisFrame) pressed = true;
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) pressed = true; // fallback
            }
            // 4. Common gamepad / cancel mapping (Submit/Cancel typical UI) via legacy
            if (!pressed && Input.GetButtonDown("Cancel")) pressed = true; // requires Input Manager mapping
            // 5. Gamepad start/back (legacy) quick check
            if (!pressed && (Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton6))) pressed = true; // Start / Back

            if (pressed && logToggleKeyDetection)
            {
                Debug.Log("[PauseMenu] Detected pause toggle input.");
            }
            return pressed;
        }

        private void WireButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(Resume);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(RestartCurrentScene);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            }
        }

        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            if (pauseTimeScale)
            {
                _prePauseTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            SetVisible(true);
        }

        public bool IsPaused => _isPaused;

        public void TogglePause()
        {
            if (_isPaused) Resume(); else Pause();
        }

        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            if (pauseTimeScale)
            {
                Time.timeScale = _prePauseTimeScale <= 0f ? 1f : _prePauseTimeScale;
            }
            SetVisible(false);
        }

        public void RestartCurrentScene()
        {
            // Keep paused overlay hidden during reload
            if (pauseTimeScale) Time.timeScale = 1f;
            _isPaused = false;
            SetVisible(false, instant:true);
            // Mark for auto restart so we go straight back into a run (shop phase) after reload instead of showing main menu.
            BulletHeavenFortressDefense.Managers.GameManager.PendingAutoRestartRun = true;
            var scene = SceneManager.GetActiveScene();
            Debug.Log("[PauseMenu] RestartCurrentScene -> reloading buildIndex=" + scene.buildIndex + " with auto-run flag.");
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void GoToMainMenu()
        {
            if (pauseTimeScale) Time.timeScale = 1f;
            _isPaused = false;
            SetVisible(false, instant:true);
            // Delegate to GameManager so we respect its loadMainMenuScene toggle & overlay logic.
            if (BulletHeavenFortressDefense.Managers.GameManager.HasInstance)
            {
                Debug.Log("[PauseMenu] GoToMainMenu -> delegating to GameManager.ReturnToMenu()");
                BulletHeavenFortressDefense.Managers.GameManager.Instance.ReturnToMenu();
            }
            else
            {
                Debug.LogWarning("[PauseMenu] GameManager not present; attempting to load scene '" + mainMenuSceneName + "'.");
                if (!string.IsNullOrEmpty(mainMenuSceneName))
                {
                    SceneManager.LoadScene(mainMenuSceneName);
                }
                else
                {
                    SceneManager.LoadScene(0);
                }
            }
        }

        private void SetVisible(bool visible, bool instant=false)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
            }
            else if (gameObject != null)
            {
                gameObject.SetActive(visible);
            }
        }

        private void EnsureUI()
        {
            if (rootCanvas != null) return;
            // Build a simple overlay
            var canvasGO = new GameObject("PauseMenu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // top
            rootCanvas = canvas;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Panel
            var panelGO = new GameObject("Panel", typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelImg = panelGO.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.72f);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Central vertical layout
            var containerGO = new GameObject("Buttons", typeof(RectTransform));
            containerGO.transform.SetParent(panelGO.transform, false);
            var contRect = containerGO.GetComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0.5f, 0.5f);
            contRect.anchorMax = new Vector2(0.5f, 0.5f);
            contRect.pivot = new Vector2(0.5f, 0.5f);
            contRect.sizeDelta = new Vector2(420f, 360f);

            resumeButton = CreateButton(containerGO.transform, "Resume", new Vector2(0, 100));
            restartButton = CreateButton(containerGO.transform, "Restart", new Vector2(0, 0));
            mainMenuButton = CreateButton(containerGO.transform, "Main Menu", new Vector2(0, -100));
        }

        private void EnsureTopRightButton()
        {
            if (_topRightButton != null) return;
            if (logButtonLifecycle) Debug.Log("[PauseMenu] Creating top-right menu button (preferHUDPlacement=" + preferHUDPlacement + ")");
            Transform parentForButton = null;
            bool embedded = false;
            if (preferHUDPlacement)
            {
                // Try to find existing WallsHealthHUD instance and use its frame
                var hud = FindObjectOfType<WallsHealthHUD>();
                if (hud != null && hud.FrameRoot != null)
                {
                    parentForButton = hud.FrameRoot;
                    embedded = true;
                    if (logButtonLifecycle) Debug.Log("[PauseMenu] Found WallsHealthHUD for embedding on first attempt.");
                }
            }

            if (!embedded)
            {
                var canvasGO = new GameObject("PauseMenu_TopRightButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGO.transform.SetParent(transform, false);
                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 4999; // just below full menu so it doesn't overdraw when paused
                _topRightCanvas = canvas;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                parentForButton = canvasGO.transform;
            }

            var btnGO = new GameObject("TopRightMenuButton", typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parentForButton, false);
            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(topRightButtonWidth, topRightButtonHeight);
            if (embedded)
            {
                // Anchor inside frame: top-right inside local space
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-hudInsetX, -hudInsetY);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-topRightButtonPadding.x, -topRightButtonPadding.y);
            }
            _topRightButton = btnGO.GetComponent<Button>();
            _topRightButton.onClick.AddListener(() => { if (_isPaused) Resume(); else Pause(); });
            if (logButtonLifecycle) Debug.Log("[PauseMenu] Top-right menu button created (embedded=" + embedded + ")");

            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(btnGO.transform, false);
            var lbl = labelGO.GetComponent<Text>();
            lbl.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.text = topRightButtonText;
            lbl.fontSize = 24;
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Public method to forcefully destroy and recreate the top-right button (e.g. after HUD rebuild).
        /// </summary>
        public void RebuildTopRightButton()
        {
            if (_topRightButton != null)
            {
                var parent = _topRightButton.transform.parent;
                Destroy(_topRightButton.gameObject);
                _topRightButton = null;
                if (parent != null && parent.name == "PauseMenu_TopRightButtonCanvas")
                {
                    Destroy(parent.gameObject); // remove temp canvas if we created one
                }
            }
            EnsureTopRightButton();
        }

        private Button CreateButton(Transform parent, string text, Vector2 localOffset)
        {
            var go = new GameObject(text + "_Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.2f, 0.9f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 80f);
            rt.anchoredPosition = localOffset;
            var btn = go.GetComponent<Button>();

            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lbl = labelGO.GetComponent<Text>();
            lbl.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.text = text;
            lbl.fontSize = 34;
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
