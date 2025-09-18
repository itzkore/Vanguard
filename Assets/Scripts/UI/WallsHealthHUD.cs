using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Color frameBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private int sortingOrder = 2000; // very high to always render on top

    private readonly List<Image> _fills = new();
    private RectTransform _gridRoot;
    private RectTransform _frameRoot;
    private Text _centerText;
    private readonly List<Text> _cellTexts = new();

        private void Awake()
        {
            EnsureCanvas();
            BuildGrid();
            // Make HUD canvas a root object to avoid DontDestroyOnLoad warning
            if (root != null && root.transform.parent != null)
            {
                root.SetParent(null, worldPositionStays: false);
            }
        DontDestroyOnLoad(root.gameObject);
        }

        private void Update()
        {
            UpdateValues();
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

            // Create a frame root (border + background)
            var frameGO = new GameObject("Frame", typeof(RectTransform));
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

            // Create a parent for the grid (inside the frame + background)
            var gridGO = new GameObject("Grid", typeof(RectTransform));
            gridGO.transform.SetParent(_frameRoot, false);
            _gridRoot = gridGO.GetComponent<RectTransform>();
            _gridRoot.anchorMin = new Vector2(0, 1);
            _gridRoot.anchorMax = new Vector2(0, 1);
            _gridRoot.pivot = new Vector2(0, 1);
            _gridRoot.anchoredPosition = new Vector2(gridPadding.x + frameBorderThickness, -(gridPadding.y + frameBorderThickness));
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
                    fRect.sizeDelta = cellSize;
                    fRect.anchoredPosition = new Vector2(c * (cellSize.x + cellSpacing.x), -r * (cellSize.y + cellSpacing.y));

                    // Inner fill (health)
                    var fillGO = new GameObject($"Cell_{r}_{c}_Fill", typeof(Image));
                    fillGO.transform.SetParent(cellFrameGO.transform, false);
                    var fillImg = fillGO.GetComponent<Image>();
                    fillImg.color = new Color(0.15f, 1f, 0.35f, 1f);
                    fillImg.raycastTarget = false;
                    var fiRect = fillGO.GetComponent<RectTransform>();
                    fiRect.anchorMin = new Vector2(0f, 0f);
                    fiRect.anchorMax = new Vector2(1f, 1f);
                    fiRect.offsetMin = new Vector2(4f, 4f);
                    fiRect.offsetMax = new Vector2(-4f, -4f);

                    _fills.Add(fillImg);
                    // Percent label per cell
                    var labelGO = new GameObject($"Cell_{r}_{c}_Label", typeof(Text));
                    labelGO.transform.SetParent(cellFrameGO.transform, false);
                    var label = labelGO.GetComponent<Text>();
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.fontSize = 12;
                    label.raycastTarget = false;
                    var lRect = label.GetComponent<RectTransform>();
                    lRect.anchorMin = new Vector2(0, 0);
                    lRect.anchorMax = new Vector2(1, 1);
                    lRect.offsetMin = Vector2.zero;
                    lRect.offsetMax = Vector2.zero;
                    _cellTexts.Add(label);
                }
            }

            // Centered percentage text for overall walls health
            var textGO = new GameObject("CenterPercent", typeof(Text));
            textGO.transform.SetParent(_gridRoot, false);
            _centerText = textGO.GetComponent<Text>();
            _centerText.alignment = TextAnchor.MiddleCenter;
            _centerText.color = Color.white;
            _centerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _centerText.fontSize = 22;
            _centerText.raycastTarget = false;
            var tRect = _centerText.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.5f);
            tRect.anchorMax = new Vector2(0.5f, 0.5f);
            tRect.pivot = new Vector2(0.5f, 0.5f);
            tRect.anchoredPosition = Vector2.zero;
            tRect.sizeDelta = new Vector2(gridW, 24f);
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

            // Center text shows average walls HP in percent
            if (_centerText != null && wallCount > 0)
            {
                int pctInt = Mathf.RoundToInt((sumPct / wallCount) * 100f);
                _centerText.text = pctInt + "%";
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
            _fills.Clear();
            _centerText = null;
            _cellTexts.Clear();
        }
    }
}