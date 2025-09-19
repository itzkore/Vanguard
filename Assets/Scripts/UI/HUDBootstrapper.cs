using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Fortress;

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
                ApplyAdaptiveHudScale();
            }
        }

        private void ApplyAdaptiveHudScale()
        {
            // Dynamically scale fontSize & referenceResolution for very large or ultrawide/tall screens to avoid tiny HUD
            var width = Screen.width; var height = Screen.height;
            float aspect = (float)width / height;
            // Base values
            int baseFont = fontSize;
            Vector2 baseRef = referenceResolution;

            // Scale factor: if resolution height > 1080 scale fonts proportionally but clamp so it doesn't get absurdly large
            float heightScale = Mathf.Clamp(height / 1080f, 1f, 1.8f);

            // If very tall ( > 19.5:9 ~ 2.16 ) we increase UI size a bit more so it stays readable
            if (aspect < 1.9f) // more square or tall (mobile portrait-like when user rotates?)
            {
                heightScale *= 1.05f;
            }

            fontSize = Mathf.RoundToInt(baseFont * heightScale);
            referenceResolution = new Vector2(baseRef.x, baseRef.y * Mathf.Clamp(heightScale,1f,1.4f));
            Debug.Log($"[HUDBootstrapper] Adaptive HUD scale applied. Screen={width}x{height} aspect={aspect:F3} fontSize={fontSize} refRes={referenceResolution}");
        }

        [ContextMenu("Ensure HUD")]
        public void EnsureHud()
        {
            if (_initialized && Application.isPlaying)
            {
                return;
            }

            EnsureEventSystem();
            EnsureBackground();
            RemoveLegacyColorBars();

            var existingCanvas = FindObjectOfType<HUDController>()?.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                RefreshExistingHud(existingCanvas);
                EnsureOverlays();
                _initialized = true;
                return;
            }

            CreateHud();
            EnsureOverlays();
            _initialized = true;
        }

        private void EnsureBackground()
        {
            // Ensure exactly one background and enforce beige palette
            var existing = FindObjectOfType<BulletHeavenFortressDefense.Visual.ParallaxBackground>();
            if (existing != null)
            {
                // Force apply beige palette in case serialized values were from an older version
                existing.ApplyDarkBrownPalette();
                return;
            }

            var go = new GameObject("ParallaxBackground");
            go.transform.SetParent(null);
            var bg = go.AddComponent<BulletHeavenFortressDefense.Visual.ParallaxBackground>();
            bg.ApplyDarkBrownPalette();
        }

        private void RemoveLegacyColorBars()
        {
            var cam = Camera.main; if (cam == null) return;
            float viewWidth = cam.orthographicSize * 2f * cam.aspect;
            float minWidth = viewWidth * 0.75f; // large enough to span most of screen
            float minHeight = cam.orthographicSize * 0.15f; // noticeable band height
            var backgrounds = FindObjectsOfType<SpriteRenderer>();
            int removed = 0;
            for (int i = 0; i < backgrounds.Length; i++)
            {
                var sr = backgrounds[i];
                if (sr == null) continue;
                if (sr.GetComponentInParent<BulletHeavenFortressDefense.Visual.ParallaxBackground>() != null) continue; // skip our new BG
                var b = sr.bounds.size;
                if (b.x < minWidth || b.y < minHeight) continue; // not a large band
                var c = sr.color;
                bool looksNavy = c.r < 0.14f && c.g < 0.16f && c.b < 0.22f; // dark desaturated blue/charcoal
                bool looksDarkRed = c.r > 0.25f && c.g < 0.16f && c.b < 0.16f; // dark muted red
                if (looksNavy || looksDarkRed)
                {
                    sr.gameObject.SetActive(false);
                    removed++;
                    Debug.Log($"[HUDBootstrapper] Disabled legacy color bar '{sr.name}' (color {c}).");
                }
            }
            if (removed > 0)
            {
                Debug.Log($"[HUDBootstrapper] Legacy color bars removed: {removed}");
            }
        }

        private void RefreshExistingHud(Canvas canvas)
        {
            var hud = canvas.GetComponent<HUDController>();
            if (hud != null)
            {
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
            RemoveChildrenWith<StatusPanelMarker>(canvas.transform);
            var oldStatusCanvas = GameObject.Find("StatusPanelCanvas");
            if (oldStatusCanvas != null)
            {
                DestroyImmediate(oldStatusCanvas);
            }

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var energyLabel = CreateBuildMenu(canvas.transform, font);
            var waveLabel = EnsureWaveLabel(canvas.transform, font);
            CreateStatusPanel(canvas.transform, font, out var baseLabel, out var phaseLabel, out var timerLabel, out var enemiesLabel, out var killsLabel);

            var hudCtl = canvas.GetComponent<HUDController>();
            if (hudCtl == null)
            {
                hudCtl = canvas.gameObject.AddComponent<HUDController>();
            }
            hudCtl.Configure(baseLabel, energyLabel, waveLabel, phaseLabel, timerLabel, enemiesLabel, killsLabel);

            CreateRunControls(canvas.transform, font);
            CreateTowerActionPanel(canvas.transform, font);
            CreateBuildOpenButton(canvas.transform, font);
            EnsureTopRightMenuButton(canvas.transform, font);
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
            // Ensure Physics2DRaycaster exists on the main camera for 2D scene object clicks
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<Physics2DRaycaster>() == null)
            {
                cam.gameObject.AddComponent<Physics2DRaycaster>();
            }

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var energyLabel = CreateBuildMenu(canvas.transform, font);
            var waveLabel = EnsureWaveLabel(canvas.transform, font);
            CreateStatusPanel(canvas.transform, font, out var baseLabel, out var phaseLabel, out var timerLabel, out var enemiesLabel, out var killsLabel);

            var hud = canvasGO.AddComponent<HUDController>();
            hud.Configure(baseLabel, energyLabel, waveLabel, phaseLabel, timerLabel, enemiesLabel, killsLabel);
            CreateRunControls(canvas.transform, font);
            CreateTowerActionPanel(canvas.transform, font);
            CreateBuildOpenButton(canvas.transform, font);
            EnsureTopRightMenuButton(canvas.transform, font);

            // Ensure zoom controller exists on the main camera so players can zoom with wheel and pinch
            if (cam != null && cam.GetComponent<BulletHeavenFortressDefense.Managers.CameraZoomController>() == null)
            {
                cam.gameObject.AddComponent<BulletHeavenFortressDefense.Managers.CameraZoomController>();
            }
        }

        private void EnsureOverlays()
        {
            // Game Over overlay (hidden until GameOver)
            if (FindObjectOfType<GameOverController>() == null)
            {
                var go = new GameObject("GameOverOverlay");
                go.transform.SetParent(null);
                go.AddComponent<GameOverController>();
            }

            // Main Menu overlay (visible only in MainMenu state)
            if (FindObjectOfType<MainMenuController>() == null)
            {
                var go = new GameObject("MainMenuOverlay");
                go.transform.SetParent(null);
                go.AddComponent<MainMenuController>();
            }
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

        private Text EnsureWaveLabel(Transform canvasRoot, Font font)
        {
            Text waveLabel = null;
            var waveNode = canvasRoot.Find("WaveText");
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
                waveLabel = CreateLabel(canvasRoot, "WaveText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), font, "Wave 0", TextAnchor.MiddleCenter);
            }

            return waveLabel;
        }

        private void CreateStatusPanel(Transform canvasRoot, Font font, out Text baseLabel, out Text phaseLabel, out Text timerLabel, out Text enemiesLabel, out Text killsLabel)
        {
            // New compact implementation: a thin horizontal bar at top-right (left of menu button)
            var statusCanvasGO = new GameObject("StatusPanelCanvas", typeof(RectTransform));
            statusCanvasGO.layer = LayerMask.NameToLayer("UI");
            var statusCanvas = statusCanvasGO.AddComponent<Canvas>();
            statusCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            statusCanvas.sortingOrder = 500;
            var statusScaler = statusCanvasGO.AddComponent<CanvasScaler>();
            statusScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            statusCanvasGO.AddComponent<GraphicRaycaster>();
            var scRect = statusCanvasGO.GetComponent<RectTransform>();
            // Full-width bottom bar: stretch across bottom
            scRect.anchorMin = new Vector2(0f, 0f);
            scRect.anchorMax = new Vector2(1f, 0f);
            scRect.pivot = new Vector2(0.5f, 0f);
            scRect.anchoredPosition = new Vector2(0f, 0f);
            scRect.sizeDelta = new Vector2(0f, 0f);

            var panelGO = new GameObject("StatusPanel", typeof(RectTransform));
            panelGO.layer = LayerMask.NameToLayer("UI");
            panelGO.AddComponent<StatusPanelMarker>();
            panelGO.AddComponent<BulletHeavenFortressDefense.UI.StatusPanelKeepAlive>();
            var rect = panelGO.GetComponent<RectTransform>();
            rect.SetParent(statusCanvasGO.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f); // flush with absolute bottom
            rect.sizeDelta = new Vector2(0f, 22f); // slimmer height (about half)

            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0f,0f,0f,0.35f); // subtle translucent

            // Horizontal layout for compact labels
            var hLayout = panelGO.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 14f;
            hLayout.padding = new RectOffset(20,20,2,2);
            hLayout.childForceExpandHeight = false;
            hLayout.childForceExpandWidth = false;

            // Helper local function to make a tiny label
            Text Make(string txt)
            {
                var t = CreateStatusLabel(panelGO.transform, font, txt);
                t.fontSize = Mathf.RoundToInt(fontSize * 0.28f); // smaller text
                t.alignment = TextAnchor.MiddleCenter;
                t.rectTransform.sizeDelta = new Vector2(0f, 16f);
                var le = t.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 80f;
                le.flexibleWidth = 0f;
                return t;
            }

            // Abbreviated labels; HUDController will overwrite texts
            baseLabel = Make("HP --");
            phaseLabel = Make("PH --");
            timerLabel = Make("NEXT --");
            enemiesLabel = Make("EN 0");
            killsLabel = Make("K 0");

            // Add a subtle divider between groups (HP/Phase/Timer | Enemies | Kills)
            void AddDivider()
            {
                var div = new GameObject("Div", typeof(RectTransform));
                var rt = div.GetComponent<RectTransform>();
                rt.SetParent(panelGO.transform, false);
                rt.sizeDelta = new Vector2(1f, 14f);
                var img = div.AddComponent<Image>();
                img.color = new Color(1f,1f,1f,0.25f);
            }
            AddDivider();
            // No special reordering needed; layout flows left->right centered.
        }

        private void PositionStatusLine(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, y);
            rt.sizeDelta = new Vector2(260f, 24f);
        }

        private void PositionStatusLineRight(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, y);
            rt.sizeDelta = new Vector2(180f, 20f);
        }

        private Text CreateStatusLabel(Transform parent, Font font, string defaultText)
        {
            var go = new GameObject("StatusLabel", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(190f, 22f);

            var label = go.AddComponent<Text>();
            label.text = defaultText;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.font = font;
            label.fontSize = Mathf.RoundToInt(fontSize * 0.5f);
            label.raycastTarget = false;

            return label;
        }

        private Text CreateLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Font font, string defaultText, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(400f, 48f);

            var label = go.AddComponent<Text>();
            label.text = defaultText;
            label.alignment = alignment;
            label.color = Color.white;
            label.font = font;
            label.fontSize = fontSize;
            label.raycastTarget = false;

            return label;
        }

        private Text CreateBuildMenu(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<BuildMenuController>(canvasRoot);

            // Create full-screen modal overlay (initially hidden)
            var overlayGO = new GameObject("TurretDialog", typeof(RectTransform));
            overlayGO.layer = LayerMask.NameToLayer("UI");
            var overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.SetParent(canvasRoot, false);
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            // Bring dialog to front
            overlayRect.SetAsLastSibling();

            var overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.5f); // dim background
            overlayImage.raycastTarget = true; // block clicks behind the dialog

            // Dialog window
            var windowGO = new GameObject("Window", typeof(RectTransform));
            windowGO.layer = LayerMask.NameToLayer("UI");
            var windowRect = windowGO.GetComponent<RectTransform>();
            windowRect.SetParent(overlayRect, false);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(540f, 680f);

            var windowBg = windowGO.AddComponent<Image>();
            windowBg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            var layout = windowGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            // Keep fixed window size; do NOT add ContentSizeFitter here, otherwise the window collapses

            var header = CreateStatusLabel(windowGO.transform, font, "Select Turret");
            header.fontSize = fontSize;
            header.alignment = TextAnchor.MiddleLeft;

            var energyLabel = CreateStatusLabel(windowGO.transform, font, "€ 0");
            // Ensure the build menu shows live currency updates
            if (energyLabel.gameObject.GetComponent<EuroDisplay>() == null)
            {
                energyLabel.gameObject.AddComponent<EuroDisplay>();
            }

            var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
            scrollGO.layer = LayerMask.NameToLayer("UI");
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.SetParent(windowGO.transform, false);
            scrollRect.sizeDelta = new Vector2(480f, 520f);
            // Ensure layout uses this preferred height
            var scrollLayout = scrollGO.AddComponent<LayoutElement>();
            scrollLayout.preferredHeight = 520f;
            scrollLayout.minHeight = 320f;
            scrollLayout.flexibleHeight = 1f;

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.layer = LayerMask.NameToLayer("UI");
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(scrollRect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.1f);

            scroll.viewport = viewportRect;

            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = LayerMask.NameToLayer("UI");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.content = contentRect;

            var menu = windowGO.AddComponent<BuildMenuController>();
            menu.Refresh();

            // Keep dialog hidden by default
            overlayGO.SetActive(false);

            // Clicking on the dim background closes the build dialog
            var dismissBtn = overlayGO.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(() =>
            {
                overlayGO.SetActive(false);
            });

            return energyLabel;
        }

        private void CreateBuildOpenButton(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<BuildOpenButtonMarker>(canvasRoot);

            // Use independent constant-pixel canvas so size doesn't scale with resolution
            var staticCanvas = EnsureStaticCanvas("StaticLowerLeftCanvas", 450, new Vector2(0f,0f));

            // Container panel holding the Turrets button and a same-sized Euro indicator
            var panelGO = new GameObject("BuildControls", typeof(RectTransform));
            panelGO.layer = LayerMask.NameToLayer("UI");
            panelGO.AddComponent<BuildOpenButtonMarker>();
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.SetParent(staticCanvas.transform, false);
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(32f, 32f);
            panelRect.sizeDelta = new Vector2(210f, 36f); // compact width for two small cells

            var hl = panelGO.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(0, 0, 0, 0);
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Turrets button
            var button = CreateButton(panelGO.transform, "Turrets", font, small:true);
            var btnLE = button.gameObject.GetComponent<LayoutElement>();
            if (btnLE != null)
            {
                btnLE.preferredWidth = 90f;
                btnLE.minWidth = 90f;
                btnLE.preferredHeight = 32f;
                btnLE.minHeight = 32f;
            }
            var btnRect = button.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(90f, 32f);
            var btnLabel = button.GetComponentInChildren<Text>();
            if (btnLabel != null)
            {
                btnLabel.fontSize = Mathf.RoundToInt(fontSize * 0.45f);
            }

            button.onClick.AddListener(() =>
            {
                var dialog = canvasRoot.Find("TurretDialog");
                if (dialog == null) return;
                var dialogGO = dialog.gameObject;
                if (!dialogGO.activeSelf)
                {
                    var ctl = dialog.Find("Window")?.GetComponent<BuildMenuController>();
                    if (ctl != null) ctl.Refresh();
                    dialog.SetAsLastSibling();
                    dialogGO.SetActive(true);
                }
                else
                {
                    dialogGO.SetActive(false);
                }
            });

            // Euro indicator (same visual style and size as button)
            var euroGO = new GameObject("EuroIndicator", typeof(RectTransform));
            euroGO.layer = LayerMask.NameToLayer("UI");
            euroGO.transform.SetParent(panelGO.transform, false);
            var euroRect = euroGO.GetComponent<RectTransform>();
            euroRect.sizeDelta = new Vector2(90f, 32f);

            var euroLE = euroGO.AddComponent<LayoutElement>();
            euroLE.preferredWidth = 90f;
            euroLE.minWidth = 90f;
            euroLE.preferredHeight = 32f;
            euroLE.minHeight = 32f;

            var euroBg = euroGO.AddComponent<Image>();
            euroBg.color = new Color(0f,0f,0f,0.95f);
            euroBg.raycastTarget = false;

            var euroLabelGO = new GameObject("Label", typeof(RectTransform));
            euroLabelGO.transform.SetParent(euroGO.transform, false);
            var euroLabelRect = euroLabelGO.GetComponent<RectTransform>();
            euroLabelRect.anchorMin = Vector2.zero;
            euroLabelRect.anchorMax = Vector2.one;
            euroLabelRect.offsetMin = Vector2.zero;
            euroLabelRect.offsetMax = Vector2.zero;

            var euroText = euroLabelGO.AddComponent<Text>();
            euroText.text = "€ 0";
            euroText.alignment = TextAnchor.MiddleCenter;
            euroText.color = Color.white;
            euroText.font = font;
            euroText.fontSize = Mathf.RoundToInt(fontSize * 0.45f);
            euroText.raycastTarget = false;

            // Auto-update on currency changes
            euroGO.AddComponent<EuroDisplay>();
        }

        private void CreateRunControls(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<RunControlPanel>(canvasRoot);
            // Use independent constant-pixel canvas (top-right-ish lower area) for static sizing
            var staticCanvas = EnsureStaticCanvas("StaticLowerRightCanvas", 450, new Vector2(1f,0f));

            var panelGO = new GameObject("RunControls", typeof(RectTransform));
            panelGO.layer = LayerMask.NameToLayer("UI");
            var rect = panelGO.GetComponent<RectTransform>();
            rect.SetParent(staticCanvas.transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            // Raise controls upward to leave space for bottom status bar (approx 40px + 8px gap)
            rect.anchoredPosition = new Vector2(-32f, 88f);
            rect.sizeDelta = new Vector2(110f, 40f);

            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0f,0f,0f,0.0f); // transparent container
            var layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var startButton = CreateButton(panelGO.transform, "Ready", font, small:true);
            var sbLE = startButton.GetComponent<LayoutElement>();
            if (sbLE != null)
            {
                sbLE.preferredWidth = 90f; sbLE.minWidth = 90f; sbLE.preferredHeight = 32f; sbLE.minHeight = 32f;
            }
            startButton.GetComponent<RectTransform>().sizeDelta = new Vector2(90f,32f);
            var sbLabel = startButton.GetComponentInChildren<Text>();
            if (sbLabel != null) sbLabel.fontSize = Mathf.RoundToInt(fontSize * 0.45f);

            var controls = panelGO.AddComponent<RunControlPanel>();
            controls.Configure(startButton);
        }

        private void EnsureTopRightMenuButton(Transform canvasRoot, Font font)
        {
            if (canvasRoot == null) return;
            if (canvasRoot.Find("TopRightMenuButton") != null) return; // already exists
            var go = new GameObject("TopRightMenuButton", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = new Vector2(1f,1f);
            rt.anchorMax = new Vector2(1f,1f);
            rt.pivot = new Vector2(1f,1f);
            rt.anchoredPosition = new Vector2(-12f,-12f);
            rt.sizeDelta = new Vector2(100f,36f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f,0f,0f,0.95f);
            img.raycastTarget = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f,0.95f,0.95f,0.9f);
            outline.effectDistance = new Vector2(1f,-1f);

            var btn = go.AddComponent<Button>();
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var txt = labelGO.AddComponent<Text>();
            txt.text = "Menu";
            txt.font = font; txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter; txt.fontSize = Mathf.RoundToInt(fontSize * 0.45f);
            // Robust click handling: if a PauseMenu does not yet exist, create one on-demand.
            btn.onClick.AddListener(() => {
                Debug.Log("[HUDMenuButton] Click -> attempting to toggle PauseMenu");
                var pm = FindObjectOfType<PauseMenu>();
                if (pm == null)
                {
                    Debug.Log("[HUDMenuButton] No PauseMenu found – creating lightweight auto instance.");
                    var pmGO = new GameObject("PauseMenu_Auto");
                    pm = pmGO.AddComponent<PauseMenu>();
                    // Ensure it starts hidden; PauseMenu Awake will create its overlay.
                }
                pm.TogglePause();
            });
        }

        private Button CreateButton(Transform parent, string text, Font font, bool small = false)
        {
            var buttonGO = new GameObject($"Button_{text}", typeof(RectTransform));
            buttonGO.layer = LayerMask.NameToLayer("UI");
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = small ? new Vector2(90f,32f) : new Vector2(140f,52f);

            var layoutElement = buttonGO.AddComponent<LayoutElement>();
            if (small)
            {
                layoutElement.preferredWidth = 90f; layoutElement.minWidth = 90f; layoutElement.preferredHeight = 32f; layoutElement.minHeight = 32f;
            }
            else
            {
                layoutElement.preferredWidth = 140f; layoutElement.minWidth = 120f; layoutElement.preferredHeight = 52f; layoutElement.minHeight = 48f;
            }

            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0f,0f,0f,0.95f);
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
            label.fontSize = Mathf.RoundToInt(fontSize * (small ? 0.45f : 0.65f));
            // Outline to mimic wall HUD border color
            var outline = buttonGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f,0.95f,0.95f,0.9f);
            outline.effectDistance = new Vector2(1f,-1f);

            return button;
        }

        private Canvas EnsureStaticCanvas(string name, int sortingOrder, Vector2 anchorCorner)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Canvas>();
            }
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorCorner;
            rt.anchorMax = anchorCorner;
            rt.pivot = anchorCorner;
            rt.anchoredPosition = Vector2.zero;
            return canvas;
        }

        private void CreateTowerActionPanel(Transform canvasRoot, Font font)
        {
            RemoveChildrenWith<TowerActionPanel>(canvasRoot);

            // Create full-screen modal overlay (hidden by default)
            var overlayGO = new GameObject("UpgradeDialog", typeof(RectTransform));
            overlayGO.layer = LayerMask.NameToLayer("UI");
            var overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.SetParent(canvasRoot, false);
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsLastSibling();

            var overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.5f); // dim background
            overlayImage.raycastTarget = true; // capture clicks

            // Window centered in screen
            var windowGO = new GameObject("Window", typeof(RectTransform));
            windowGO.layer = LayerMask.NameToLayer("UI");
            var windowRect = windowGO.GetComponent<RectTransform>();
            windowRect.SetParent(overlayRect, false);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(680f, 480f); // further enlarged

            var bg = windowGO.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            var layout = windowGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fitter = windowGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text MakeLbl(string defaultText)
            {
                var go = new GameObject("Label", typeof(RectTransform));
                go.layer = LayerMask.NameToLayer("UI");
                var t = go.AddComponent<Text>();
                t.text = defaultText;
                t.alignment = TextAnchor.MiddleLeft;
                t.color = Color.white;
                t.font = font;
                t.fontSize = Mathf.RoundToInt(fontSize * 0.6f);
                var r = go.GetComponent<RectTransform>();
                // Parent to the centered upgrade window
                r.SetParent(windowGO.transform, false);
                r.sizeDelta = new Vector2(220f, 32f);
                return t;
            }

            var title = MakeLbl("Selected: --");
            var level = MakeLbl("Level: 1");
            var invested = MakeLbl("Invested: € 0");
            var stats = MakeLbl("Stats\nFR: --\nRange: --\nDamage: --");
            // Give stats block more height
            var statsRect = stats.GetComponent<RectTransform>();
            statsRect.sizeDelta = new Vector2(600f, 220f);

            var upgradeBtn = CreateButton(windowGO.transform, "Upgrade", font);
            var upgradeLbl = upgradeBtn.GetComponentInChildren<Text>();
            var sellBtn = CreateButton(windowGO.transform, "Sell", font);
            var sellLbl = sellBtn.GetComponentInChildren<Text>();

            // Widen buttons to accommodate long cost strings (e.g., large upgrade prices)
            var upRect = upgradeBtn.GetComponent<RectTransform>();
            upRect.sizeDelta = new Vector2(640f, upRect.sizeDelta.y);
            var upLE = upgradeBtn.GetComponent<LayoutElement>();
            if (upLE != null)
            {
                upLE.preferredWidth = 640f;
                upLE.minWidth = 480f;
            }
            if (upgradeLbl != null)
            {
                upgradeLbl.resizeTextForBestFit = true;
                upgradeLbl.resizeTextMinSize = Mathf.RoundToInt(fontSize * 0.35f);
            }

            var sellRect = sellBtn.GetComponent<RectTransform>();
            sellRect.sizeDelta = new Vector2(420f, sellRect.sizeDelta.y);
            var sellLE = sellBtn.GetComponent<LayoutElement>();
            if (sellLE != null)
            {
                sellLE.preferredWidth = 420f;
                sellLE.minWidth = 300f;
            }

            var widget = windowGO.AddComponent<TowerActionPanel>();
            // Assign private serialized fields using reflection to avoid public API changes
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            widget.GetType().GetField("titleText", flags)?.SetValue(widget, title);
            widget.GetType().GetField("levelText", flags)?.SetValue(widget, level);
            widget.GetType().GetField("upgradeButton", flags)?.SetValue(widget, upgradeBtn);
            widget.GetType().GetField("upgradeLabel", flags)?.SetValue(widget, upgradeLbl);
            widget.GetType().GetField("sellButton", flags)?.SetValue(widget, sellBtn);
            widget.GetType().GetField("sellLabel", flags)?.SetValue(widget, sellLbl);
            widget.GetType().GetField("investedText", flags)?.SetValue(widget, invested);
            widget.GetType().GetField("statsText", flags)?.SetValue(widget, stats);

            // Hidden until selection
            overlayGO.SetActive(false);

            // Clicking on the dim background (outside window) closes the dialog
            var dismissBtn = overlayGO.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(() =>
            {
                widget.Deselect();
                overlayGO.SetActive(false);
            });

            var sel = FindObjectOfType<Managers.SelectionManager>();
            if (sel == null)
            {
                var go = new GameObject("SelectionManager");
                sel = go.AddComponent<Managers.SelectionManager>();
            }
            sel.Configure(widget);

            // Ensure a simple Repair dialog exists too
            CreateRepairDialog(canvasRoot, font);
        }

        private void CreateRepairDialog(Transform canvasRoot, Font font)
        {
            // Avoid duplicates
            var existing = canvasRoot.Find("RepairDialog");
            if (existing != null) return;

            // Modal overlay
            var overlayGO = new GameObject("RepairDialog", typeof(RectTransform));
            overlayGO.layer = LayerMask.NameToLayer("UI");
            var overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.SetParent(canvasRoot, false);
            overlayRect.anchorMin = new Vector2(0f, 0f);
            overlayRect.anchorMax = new Vector2(1f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsLastSibling();

            var overlayImage = overlayGO.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.5f);
            overlayImage.raycastTarget = true;

            // Window
            var windowGO = new GameObject("Window", typeof(RectTransform));
            windowGO.layer = LayerMask.NameToLayer("UI");
            var windowRect = windowGO.GetComponent<RectTransform>();
            windowRect.SetParent(overlayRect, false);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(320f, 160f);

            var bg = windowGO.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            var layout = windowGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;

            Text MakeLbl(string defaultText)
            {
                var go = new GameObject("Label", typeof(RectTransform));
                go.layer = LayerMask.NameToLayer("UI");
                var t = go.AddComponent<Text>();
                t.text = defaultText;
                t.alignment = TextAnchor.MiddleLeft;
                t.color = Color.white;
                t.font = font;
                t.fontSize = Mathf.RoundToInt(fontSize * 0.6f);
                var r = go.GetComponent<RectTransform>();
                r.SetParent(windowGO.transform, false);
                r.sizeDelta = new Vector2(220f, 32f);
                return t;
            }

            var title = MakeLbl("Repair Wall?");
            var cost = MakeLbl("Cost: € 0");
            var ok = CreateButton(windowGO.transform, "Repair", font);
            var cancel = CreateButton(windowGO.transform, "Cancel", font);

            // Controller component to handle open/close and assign target wall
            var ctl = windowGO.AddComponent<WallRepairDialog>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            ctl.GetType().GetField("titleText", flags)?.SetValue(ctl, title);
            ctl.GetType().GetField("costText", flags)?.SetValue(ctl, cost);
            ctl.GetType().GetField("confirmButton", flags)?.SetValue(ctl, ok);
            ctl.GetType().GetField("cancelButton", flags)?.SetValue(ctl, cancel);

            overlayGO.SetActive(false);

            // Dismiss on outside click
            var dismissBtn = overlayGO.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(() =>
            {
                ctl.Close();
            });

            // Ensure a click handler exists on walls to open this dialog
            var fm = FindObjectOfType<FortressManager>();
            if (fm != null)
            {
                var walls = fm.GetActiveWalls();
                if (walls != null)
                {
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var w = walls[i];
                        if (w == null) continue;
                        var click = w.gameObject.GetComponent<WallClickHandler>();
                        if (click == null) click = w.gameObject.AddComponent<WallClickHandler>();
                        click.Configure(ctl);
                    }
                }
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

        private class StatusPanelMarker : MonoBehaviour { }
        private class BuildOpenButtonMarker : MonoBehaviour { }
    }
}
