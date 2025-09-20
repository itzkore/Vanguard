using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Fortress;

namespace BulletHeavenFortressDefense.UI
{
    public class WallsHealthHUD : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Vector2 padding = new Vector2(12, 12);
        [SerializeField] private Vector2 cellSize = new Vector2(28, 28); // frame outer size (larger)
        [SerializeField] private Vector2 cellSpacing = new Vector2(3, 3);
        [SerializeField] private Vector2 gridPadding = new Vector2(8, 8);
        [Header("Frame")]
        [SerializeField] private float frameBorderThickness = 3f;
        [SerializeField] private Color frameBorderColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
        [SerializeField] private Color frameBackgroundColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private int sortingOrder = 2000; // very high to always render on top
        [Header("Visual Behavior")]
        [SerializeField] private bool smoothFillLerp = true;
        [SerializeField, Range(1f, 20f)] private float fillLerpSpeed = 8f;

        private readonly List<Image> _fills = new();
        private readonly List<Image> _fillBGs = new();
        private readonly List<float> _targetFill = new();
        private RectTransform _gridRoot;
        private RectTransform _frameRoot;
        private RectTransform _contentRoot; // inner content area inside the frame (padded by border + grid padding)
    private readonly List<Text> _cellTexts = new();
    [Header("Repair Bar Placement")]
    [SerializeField, Tooltip("Place repair bar directly to the right of the wall HUD frame (top-aligned). If false, it's below frame (legacy layout). ")] private bool repairBarTopRight = true;
    [SerializeField, Tooltip("Horizontal spacing between frame and repair bar when top-right.")] private float repairBarGridSpacingX = 12f;
    [SerializeField, Tooltip("Vertical pixel offset relative to frame top when top-right (positive moves down). ")] private float repairBarVerticalOffset = 0f;
    // Repair All UI
    private Button _repairAllButton;
    private Text _repairAllLabel;
    private Text _repairAllCostLabel;

        private void Awake()
        {
            EnsureCanvas();
            CaptureBaselines();
            ApplyScaleIfAvailable();
            BuildGrid();
            // Make HUD canvas a root object to avoid DontDestroyOnLoad warning
            if (root != null && root.transform.parent != null)
            {
                root.SetParent(null, worldPositionStays: false);
            }
            DontDestroyOnLoad(root.gameObject);
            ApplyVisibility(GameManager.HasInstance ? GameManager.Instance.CurrentState : GameManager.GameState.MainMenu);
        }

        private Vector2 _basePadding;
        private Vector2 _baseCellSize;
        private Vector2 _baseCellSpacing;
        private Vector2 _baseGridPadding;
        private float _baseFrameBorderThickness;
        private bool _baselineCaptured = false;
        private int _lastAppliedScaleVersion = -1;

        private void CaptureBaselines()
        {
            if (_baselineCaptured) return;
            _basePadding = padding;
            _baseCellSize = cellSize;
            _baseCellSpacing = cellSpacing;
            _baseGridPadding = gridPadding;
            _baseFrameBorderThickness = frameBorderThickness;
            _baselineCaptured = true;
        }

        private void ApplyScaleIfAvailable()
        {
            // Only scale in landscape; if portrait just leave originals
            if (Screen.width < Screen.height) return;
            var sx = BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedScaleX;
            var sy = BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedScaleY;
            if (sx <= 0f || sy <= 0f) return;
            // Prevent re-applying same scale version repeatedly (unless we add live rescale later)
            if (_lastAppliedScaleVersion == BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedScaleVersion) return;
            _lastAppliedScaleVersion = BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedScaleVersion;

            // Revert to baselines first to avoid compounding
            padding = _basePadding;
            cellSize = _baseCellSize;
            cellSpacing = _baseCellSpacing;
            gridPadding = _baseGridPadding;
            frameBorderThickness = _baseFrameBorderThickness;

            padding = new Vector2(padding.x * sx, padding.y * sy);
            cellSize = new Vector2(cellSize.x * sx, cellSize.y * sy);
            cellSpacing = new Vector2(cellSpacing.x * sx, cellSpacing.y * sy);
            gridPadding = new Vector2(gridPadding.x * sx, gridPadding.y * sy);
            frameBorderThickness = frameBorderThickness * sy; // thickness ties to vertical scale

            if (root != null)
            {
                root.anchoredPosition = new Vector2(padding.x, -padding.y);
            }
        }

        private void Update()
        {
            UpdateValues();
            // Continuously ensure repair bar stays positioned relative to frame (frame can resize on scale changes)
            if (_repairBarRoot != null && _frameRoot != null)
            {
                PositionRepairBar();
            }
            // Optionally respond to runtime HUD scale recalculations
            if (_baselineCaptured && _lastAppliedScaleVersion != BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedScaleVersion)
            {
                // Rebuild everything at new scale
                ApplyScaleIfAvailable();
                BuildGrid();
            }
            // Safety net: if repair UI somehow destroyed (scene reload order, etc), rebuild once
            if ((_repairAllButton == null || _repairAllCostLabel == null || _repairAllLabel == null) && _frameRoot != null)
            {
                BuildRepairAllUI();
            }
            else if (_repairAllButton == null)
            {
                // If frame missing too, attempt full rebuild occasionally
                _repairHealTimer += Time.unscaledDeltaTime;
                if (_repairHealTimer >= 1.25f)
                {
                    _repairHealTimer = 0f;
                    if (!_repairLoggedMissing)
                    {
                        Debug.LogWarning("[WallsHealthHUD] Repair bar missing (frame likely destroyed). Attempting full grid rebuild.");
                        _repairLoggedMissing = true;
                    }
                    BuildGrid();
                }
            }
            if (smoothFillLerp)
            {
                for (int i = 0; i < _fills.Count; i++)
                {
                    var img = _fills[i];
                    if (img == null) continue;
                    float target = i < _targetFill.Count ? _targetFill[i] : img.fillAmount;
                    if (!Mathf.Approximately(img.fillAmount, target))
                    {
                        img.fillAmount = Mathf.MoveTowards(img.fillAmount, target, fillLerpSpeed * Time.deltaTime);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged += OnGameStateChanged;
                ApplyVisibility(GameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StateChanged -= OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            ApplyVisibility(state);
        }

        private void ApplyVisibility(GameManager.GameState state)
        {
            // Rules revision:
            // We want the HUD visible for all in-run phases (Shop, Preparation, Combat) AND persist during transitions
            // where GameManager might briefly enter Completed/GameOver AFTER enemies finish but before user returns.
            // Hide only in MainMenu and Boot.
            bool show = state != GameManager.GameState.MainMenu && state != GameManager.GameState.Boot;
            // If GameOver or Completed: keep showing so player can still see wall aftermath until returning to menu.
            if (root != null)
            {
                if (!root.gameObject.activeSelf && show)
                {
                    root.gameObject.SetActive(true);
                }
                else if (root.gameObject.activeSelf && !show)
                {
                    root.gameObject.SetActive(false);
                }
            }
            else
            {
                if (!gameObject.activeSelf && show) gameObject.SetActive(true);
                else if (gameObject.activeSelf && !show) gameObject.SetActive(false);
            }

            // Defensive: if we are supposed to show but the frame was somehow destroyed, rebuild immediately.
            if (show && (_frameRoot == null || _fills.Count == 0))
            {
                BuildGrid();
            }
        }

        private void EnsureCanvas()
        {
            if (root != null) return;
            var canvasGO = new GameObject("WallsHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            root = canvasGO.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0, 1);
            root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(padding.x, -padding.y);
        }

        private void BuildGrid()
        {
            ClearGrid();
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm == null) return;
            int rows = Mathf.Max(1, fm.Rows);
            int cols = Mathf.Max(1, fm.Columns);

            // Size grid root so we can center the percentage text inside
            float gridW = cols * cellSize.x + (cols - 1) * cellSpacing.x;
            float gridH = rows * cellSize.y + (rows - 1) * cellSpacing.y;

            // Create a frame root (border + background) — this is the parent of the HUD
            var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(RectMask2D));
            frameGO.transform.SetParent(root, false);
            _frameRoot = frameGO.GetComponent<RectTransform>();
            _frameRoot.anchorMin = new Vector2(0, 1);
            _frameRoot.anchorMax = new Vector2(0, 1);
            _frameRoot.pivot = new Vector2(0, 1);
            float outerW = gridW + 2f * (gridPadding.x + frameBorderThickness);
            float outerH = gridH + 2f * (gridPadding.y + frameBorderThickness);
            _frameRoot.sizeDelta = new Vector2(outerW, outerH);
            _frameRoot.anchoredPosition = Vector2.zero;

            // Border lines (explicit 4 sides)
            CreateBorderLine("BorderTop",   new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -frameBorderThickness));
            CreateBorderLine("BorderBottom",new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, frameBorderThickness));
            CreateBorderLine("BorderLeft",  new Vector2(0, 0), new Vector2(0, 1), new Vector2(frameBorderThickness, 0));
            CreateBorderLine("BorderRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-frameBorderThickness, 0));

            // Background image (inside border)
            var bgGO = new GameObject("Background", typeof(Image));
            bgGO.transform.SetParent(_frameRoot, false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = frameBackgroundColor;
            bgImg.raycastTarget = false;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(1, 1);
            bgRect.offsetMin = new Vector2(frameBorderThickness, frameBorderThickness);
            bgRect.offsetMax = new Vector2(-frameBorderThickness, -frameBorderThickness);

            // Create a content root that applies padding (border + gridPadding)
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(_frameRoot, false);
            _contentRoot = contentGO.GetComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0, 0);
            _contentRoot.anchorMax = new Vector2(1, 1);
            _contentRoot.pivot = new Vector2(0, 1);
            _contentRoot.offsetMin = new Vector2(frameBorderThickness + gridPadding.x, frameBorderThickness + gridPadding.y);
            _contentRoot.offsetMax = new Vector2(-(frameBorderThickness + gridPadding.x), -(frameBorderThickness + gridPadding.y));

            // Create a parent for the grid (inside the content)
            var gridGO = new GameObject("Grid", typeof(RectTransform));
            gridGO.transform.SetParent(_contentRoot, false);
            _gridRoot = gridGO.GetComponent<RectTransform>();
            _gridRoot.anchorMin = new Vector2(0, 1);
            _gridRoot.anchorMax = new Vector2(0, 1);
            _gridRoot.pivot = new Vector2(0, 1);
            _gridRoot.anchoredPosition = Vector2.zero;
            _gridRoot.sizeDelta = new Vector2(gridW, gridH);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Skip core cell entirely (no frame, no fill)
                    if (r == fm.CoreRow && c == fm.CoreColumn)
                    {
                        continue;
                    }
                    // Frame (per-cell)
                    var cellFrameGO = new GameObject($"Cell_{r}_{c}_Frame", typeof(Image));
                    cellFrameGO.transform.SetParent(_gridRoot, false);
                    var frameImg = cellFrameGO.GetComponent<Image>();
                    frameImg.color = new Color(0.8f, 0.8f, 0.8f, 0.45f); // grey transparent frame fill
                    frameImg.raycastTarget = false;
                    var fRect = cellFrameGO.GetComponent<RectTransform>();
                    // Ensure top-left anchoring for predictable layout
                    fRect.anchorMin = new Vector2(0, 1);
                    fRect.anchorMax = new Vector2(0, 1);
                    fRect.pivot = new Vector2(0, 1);
                    fRect.sizeDelta = cellSize;
                    fRect.anchoredPosition = new Vector2(c * (cellSize.x + cellSpacing.x), -r * (cellSize.y + cellSpacing.y));

                    // Background (black) inside frame
                    var bgGO2 = new GameObject($"Cell_{r}_{c}_BG", typeof(Image));
                    bgGO2.transform.SetParent(cellFrameGO.transform, false);
                    var bgImg2 = bgGO2.GetComponent<Image>();
                    bgImg2.color = Color.black;
                    bgImg2.raycastTarget = false;
                    var bg2Rect = bgGO2.GetComponent<RectTransform>();
                    bg2Rect.anchorMin = new Vector2(0f,0f);
                    bg2Rect.anchorMax = new Vector2(1f,1f);
                    bg2Rect.offsetMin = new Vector2(4f,4f);
                    bg2Rect.offsetMax = new Vector2(-4f,-4f);

                    // Foreground fill on top
                    var fillGO = new GameObject($"Cell_{r}_{c}_Fill", typeof(Image));
                    fillGO.transform.SetParent(cellFrameGO.transform, false);
                    var fillImg = fillGO.GetComponent<Image>();
                    fillImg.color = new Color(0.15f, 1f, 0.35f, 1f);
                    fillImg.raycastTarget = false;
                    fillImg.type = Image.Type.Filled;
                    fillImg.fillMethod = Image.FillMethod.Horizontal;
                    fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                    fillImg.fillAmount = 1f;
                    var fiRect = fillGO.GetComponent<RectTransform>();
                    fiRect.anchorMin = new Vector2(0f, 0f);
                    fiRect.anchorMax = new Vector2(1f, 1f);
                    fiRect.offsetMin = new Vector2(4f, 4f);
                    fiRect.offsetMax = new Vector2(-4f, -4f);

                    _fills.Add(fillImg);
                    _fillBGs.Add(bgImg2);
                    _targetFill.Add(1f);
                    // Percent label per cell
                    var labelGO = new GameObject($"Cell_{r}_{c}_Label", typeof(Text));
                    labelGO.transform.SetParent(cellFrameGO.transform, false);
                    var label = labelGO.GetComponent<Text>();
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    label.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
                    // Scale font relative to HUD published font base (12 baseline of cell label ~ 0.33 of menu baseline 36)
                    int baseFont = BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedFontBase;
                    label.fontSize = Mathf.RoundToInt(baseFont * 0.33f);
                    label.raycastTarget = false;
                    var lRect = label.GetComponent<RectTransform>();
                    lRect.anchorMin = new Vector2(0, 0);
                    lRect.anchorMax = new Vector2(1, 1);
                    lRect.offsetMin = Vector2.zero;
                    lRect.offsetMax = Vector2.zero;
                    _cellTexts.Add(label);
                }
            }

            // Add Repair All section (top-right or below frame depending on setting)
            BuildRepairAllUI();
        }

        private void CreateBorderLine(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 _)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(_frameRoot, false);
            var img = go.GetComponent<Image>();
            img.color = frameBorderColor;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // If horizontal line (top/bottom): set height via thickness, full width
            if (Mathf.Approximately(anchorMin.y, anchorMax.y))
            {
                rt.offsetMin = new Vector2(0, anchorMin.y == 0 ? 0 : -frameBorderThickness);
                rt.offsetMax = new Vector2(0, anchorMin.y == 0 ? frameBorderThickness : 0);
            }
            // If vertical line (left/right): set width via thickness, full height
            else if (Mathf.Approximately(anchorMin.x, anchorMax.x))
            {
                rt.offsetMin = new Vector2(anchorMin.x == 0 ? 0 : -frameBorderThickness, 0);
                rt.offsetMax = new Vector2(anchorMin.x == 0 ? frameBorderThickness : 0, 0);
            }
        }

        private void UpdateValues()
        {
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm == null) return;
            var walls = fm.GetActiveWalls();
            if (walls == null) return;
            // Rebuild if grid size changed or count changed
            int totalCells = Mathf.Max(1, fm.Rows * fm.Columns) - 1; // minus core cell
            if (_fills.Count != totalCells)
            {
                BuildGrid();
            }

            // Map walls into a rows x cols grid (skip the core cell)
            // We expect walls.Count == rows*cols - 1
            int rows = Mathf.Max(1, fm.Rows);
            int cols = Mathf.Max(1, fm.Columns);
            int coreR = Mathf.Clamp(fm.CoreRow, 0, rows - 1);
            int coreC = Mathf.Clamp(fm.CoreColumn, 0, cols - 1);

            int fillIndex = 0;
            int textIndex = 0;
            float sumPct = 0f;
            int wallCount = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (r == coreR && c == coreC)
                    {
                        // skip core cell (no wall)
                        continue;
                    }

                    if (fillIndex >= _fills.Count)
                    {
                        break;
                    }

                    var fillImg = _fills[fillIndex++];
                    var label = (textIndex < _cellTexts.Count) ? _cellTexts[textIndex++] : null;
                    // Find wall for this exact grid cell by Row/Column
                    FortressWall matched = null;
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var w = walls[i];
                        if (w != null && w.Row == r && w.Column == c)
                        {
                            matched = w;
                            break;
                        }
                    }

                    float pct = (matched != null && matched.MaxHealth > 0)
                        ? Mathf.Clamp01((float)matched.CurrentHealth / matched.MaxHealth)
                        : 0f;
                    if (smoothFillLerp)
                    {
                        if (fillIndex-1 < _targetFill.Count) _targetFill[fillIndex-1] = pct;
                    }
                    else
                    {
                        fillImg.fillAmount = pct;
                    }
                    fillImg.color = EvaluateDamageColor(pct);
                    if (label != null)
                    {
                        int pctInt = Mathf.RoundToInt(pct * 100f);
                        label.text = pctInt.ToString();
                    }
                    sumPct += pct;
                    wallCount++;
                }
            }

            // Update Repair All UI
            if (_repairAllCostLabel != null && walls != null)
            {
                int totalCost = 0;
                for (int i = 0; i < walls.Count; i++)
                {
                    var w = walls[i];
                    if (w == null) continue;
                    if (w.CurrentHealth >= w.MaxHealth) continue;
                    totalCost += w.GetRepairCostForMissing();
                }
                int energy = Systems.EconomySystem.HasInstance ? Systems.EconomySystem.Instance.CurrentEnergy : 0;
                UpdateRepairAllVisual(totalCost, energy);
            }
        }

        private static Color EvaluateDamageColor(float percent)
        {
            percent = Mathf.Clamp01(percent);
            if (percent >= 0.66f)
            {
                float t = Mathf.InverseLerp(1f, 0.66f, percent);
                return Color.Lerp(new Color(0.15f, 1f, 0.35f, 1f), new Color(1f, 0.9f, 0.2f, 1f), t);
            }
            else
            {
                float t = Mathf.InverseLerp(0.66f, 0f, percent);
                return Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.2f, 0.2f, 1f), t);
            }
        }

        private void ClearGrid()
        {
            if (_gridRoot != null)
            {
                Destroy(_gridRoot.gameObject);
                _gridRoot = null;
            }
            if (_frameRoot != null)
            {
                Destroy(_frameRoot.gameObject);
                _frameRoot = null;
            }
            if (_contentRoot != null)
            {
                Destroy(_contentRoot.gameObject);
                _contentRoot = null;
            }
            _fills.Clear();
            _cellTexts.Clear();
            if (_repairAllButton != null)
            {
                Destroy(_repairAllButton.gameObject.transform.parent.gameObject);
                _repairAllButton = null;
                _repairAllLabel = null;
                _repairAllCostLabel = null;
            }
        }

        private RectTransform _repairBarRoot;

    /// <summary>
    /// Public read-only access to the outer frame RectTransform so that other HUD elements
    /// (like a pause/menu button) can anchor themselves precisely "inside" or adjacent to the
    /// walls HUD without duplicating layout calculations.
    /// </summary>
    public RectTransform FrameRoot => _frameRoot;

        private void BuildRepairAllUI()
        {
            var containerGO = new GameObject("RepairAllBar", typeof(RectTransform));
            containerGO.transform.SetParent(root, false);
            var rt = containerGO.GetComponent<RectTransform>();
            _repairBarRoot = rt;
            if (repairBarTopRight)
            {
                // Same top-left anchoring as frame so we can position relative easily
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
            }
            rt.sizeDelta = new Vector2(340f, 40f); // larger, more readable container

            // Defensive: clear old references if any partially exist
            _repairAllButton = null; _repairAllLabel = null; _repairAllCostLabel = null;

            // Button (pill style relies on default UISprite)
            var btnGO = new GameObject("RepairAllButton", typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rt, false);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.color = new Color(0.12f, 0.55f, 0.18f, 0.95f);
            btnImg.raycastTarget = true;
            var btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.sizeDelta = new Vector2(150f, 0f); // wider button
            btnRt.anchoredPosition = new Vector2(0f, 0f);
            _repairAllButton = btnGO.GetComponent<Button>();
            _repairAllButton.onClick.AddListener(RepairAllClicked);

            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(btnGO.transform, false);
            _repairAllLabel = labelGO.GetComponent<Text>();
            _repairAllLabel.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
            _repairAllLabel.text = "Repair"; // shorter
            _repairAllLabel.alignment = TextAnchor.MiddleCenter;
            _repairAllLabel.color = Color.white;
            _repairAllLabel.fontSize = Mathf.RoundToInt(BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedFontBase * 0.52f); // even bigger for visibility
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            // Cost label (inline to right)
            var costGO = new GameObject("Cost", typeof(Text));
            costGO.transform.SetParent(rt, false);
            _repairAllCostLabel = costGO.GetComponent<Text>();
            _repairAllCostLabel.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
            _repairAllCostLabel.text = "€0";
            _repairAllCostLabel.alignment = TextAnchor.MiddleLeft;
            _repairAllCostLabel.color = Color.white;
            _repairAllCostLabel.fontSize = Mathf.RoundToInt(BulletHeavenFortressDefense.UI.HUDBootstrapper.PublishedFontBase * 0.48f);
            var costRt = costGO.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0f, 0f);
            costRt.anchorMax = new Vector2(0f, 1f);
            costRt.pivot = new Vector2(0f, 0.5f);
            costRt.sizeDelta = new Vector2(180f, 0f);
            costRt.anchoredPosition = new Vector2(160f, 0f);
            PositionRepairBar();
        }

        // --- Self-heal tracking for repair bar ---
        private float _repairHealTimer = 0f;
        private bool _repairLoggedMissing = false;

        private void PositionRepairBar()
        {
            if (_repairBarRoot == null) return;
            if (_frameRoot == null)
            {
                // fallback: leave where created
                return;
            }

            if (repairBarTopRight)
            {
                // Place immediately to right of frame
                float x = _frameRoot.sizeDelta.x + repairBarGridSpacingX;
                float y = repairBarVerticalOffset; // frame top reference
                _repairBarRoot.anchoredPosition = new Vector2(x, -y);
            }
            else
            {
                float yOffset = (_frameRoot.sizeDelta.y + 4f);
                _repairBarRoot.anchoredPosition = new Vector2(0f, -yOffset);
            }
        }

        private void UpdateRepairAllVisual(int totalCost, int energy)
        {
            if (_repairAllButton == null || _repairAllLabel == null || _repairAllCostLabel == null) return;
            // Phase gating: only allow repairs in Shop or Preparation phases
            bool buildPhase = false;
            if (WaveManager.HasInstance)
            {
                var phase = WaveManager.Instance.CurrentPhase;
                buildPhase = phase == WaveManager.WavePhase.Shop || phase == WaveManager.WavePhase.Preparation;
            }

            if (!buildPhase)
            {
                // During combat or other phases: disable and show message
                _repairAllButton.interactable = false;
                _repairAllLabel.text = "Repair"; // keep action name
                if (totalCost > 0)
                {
                    _repairAllCostLabel.text = "(Between waves)";
                }
                else
                {
                    _repairAllCostLabel.text = ""; // nothing needed if fully repaired anyway
                }
                var img = _repairAllButton.GetComponent<Image>();
                if (img) img.color = new Color(0.18f, 0.18f, 0.18f, 0.55f);
                return;
            }

            if (totalCost <= 0)
            {
                _repairAllButton.interactable = false;
                _repairAllLabel.text = "Repaired";
                _repairAllCostLabel.text = ""; // no extra text when fully repaired
                var img = _repairAllButton.GetComponent<Image>();
                if (img) img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
                return;
            }

            bool canAfford = energy >= totalCost;
            _repairAllButton.interactable = canAfford;
            _repairAllLabel.text = "Repair";
            _repairAllCostLabel.text = canAfford ? $"€ {totalCost}" : $"€ {totalCost}  (You: {energy})";
            _repairAllCostLabel.color = canAfford ? new Color(0.5f, 0.95f, 0.6f, 1f) : new Color(1f, 0.4f, 0.3f, 1f);
            var btnImg = _repairAllButton.GetComponent<Image>();
            if (btnImg)
            {
                btnImg.color = canAfford ? new Color(0.12f, 0.55f, 0.18f, 0.95f) : new Color(0.25f, 0.25f, 0.25f, 0.9f);
            }
        }

        private void RepairAllClicked()
        {
            // Prevent repairs during combat phases
            if (WaveManager.HasInstance)
            {
                var phase = WaveManager.Instance.CurrentPhase;
                bool build = phase == WaveManager.WavePhase.Shop || phase == WaveManager.WavePhase.Preparation;
                if (!build)
                {
                    HUDController.Toast("Repair only between waves");
                    return;
                }
            }
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm == null) return;
            var walls = fm.GetActiveWalls();
            if (walls == null) return;
            int totalCost = 0;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (w == null) continue;
                if (w.CurrentHealth >= w.MaxHealth) continue;
                totalCost += w.GetRepairCostForMissing();
            }
            if (totalCost <= 0) return;
            if (!Systems.EconomySystem.HasInstance || Systems.EconomySystem.Instance.CurrentEnergy < totalCost) return;
            if (!Systems.EconomySystem.Instance.TrySpend(totalCost)) return;
            // Perform actual repairs WITHOUT additional cost (ForceFullRepair)
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (w == null) continue;
                if (w.CurrentHealth >= w.MaxHealth) continue;
                w.ForceFullRepair();
            }
        }
    }
}