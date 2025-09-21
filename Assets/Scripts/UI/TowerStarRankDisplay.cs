using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Displays a row of star icons above the tower to visualize its current level (rank).
    /// Stars fill up to current level; max star count limited to configured max (e.g., 10).
    /// </summary>
    public class TowerStarRankDisplay : MonoBehaviour
    {
        [SerializeField] private TowerBehaviour tower; // auto-assign if null
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;
        [SerializeField, Tooltip("Optional dynamic load from Resources/UI/star_filled")] private bool autoLoadSprites = true;
    [SerializeField, Range(4,16)] private int maxStars = 10;
        [SerializeField, Range(0.01f,1f)] private float starSpacing = 0.12f; // world units spacing
        [SerializeField, Tooltip("Vertical offset above tower origin in world units.")] private float verticalOffset = 1.2f;
        [SerializeField, Tooltip("Scale factor for each star.")] private float starScale = 0.35f;
        [SerializeField, Tooltip("Hide when level == 1 (optional)")] private bool hideAtBaseLevel = false;
    [SerializeField, Tooltip("Billboard stars full 3D toward camera.")] private bool billboard = true;
    [SerializeField, Tooltip("If set, only rotate around Y (top-down). Useful for orthographic top view.")] private bool yAxisOnly = true;
    [SerializeField, Tooltip("Fallback: render unicode stars (★ ☆) if sprites missing.")] private bool allowTextFallback = true;
    [SerializeField, Tooltip("Text fallback font size (world space). Only used if sprites absent.")] private int textFallbackSize = 32;
    [SerializeField, Tooltip("Add subtle outline to star sprites.")] private bool addOutline = false;

    private bool _usingTextFallback = false;

        private Camera _cam;
        private readonly System.Collections.Generic.List<Image> _stars = new System.Collections.Generic.List<Image>(16);
        private Canvas _canvas;

        private void Awake()
        {
            if (tower == null) tower = GetComponent<TowerBehaviour>();
            if (autoLoadSprites)
            {
                if (starFilled == null) starFilled = Resources.Load<Sprite>("UI/star_filled");
                if (starEmpty == null) starEmpty = Resources.Load<Sprite>("UI/star_empty");
            }
            CreateCanvas();
            BuildStars();
            Refresh();
        }

        private void OnEnable()
        {
            if (tower != null)
            {
                tower.StatsRecalculated += OnStats;
            }
        }
        private void OnDisable()
        {
            if (tower != null)
            {
                tower.StatsRecalculated -= OnStats;
            }
        }

        private void LateUpdate()
        {
            if (billboard && _canvas != null)
            {
                if (_cam == null) _cam = Camera.main;
                if (_cam != null)
                {
                    if (yAxisOnly)
                    {
                        Vector3 camPos = _cam.transform.position;
                        Vector3 dir = _canvas.transform.position - camPos;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.001f)
                            _canvas.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    }
                    else
                    {
                        _canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - _cam.transform.position);
                    }
                }
            }
            if (tower != null)
            {
                var basePos = tower.transform.position + Vector3.up * verticalOffset;
                _canvas.transform.position = basePos;
            }
        }

        private void OnStats(TowerBehaviour t)
        {
            Refresh();
        }

        private void CreateCanvas()
        {
            var go = new GameObject("StarCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 32f;
            go.AddComponent<GraphicRaycaster>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(maxStars, 1f);
        }

        private void BuildStars()
        {
            _usingTextFallback = (starFilled == null || starEmpty == null) && allowTextFallback;

            // Determine total width to center align
            float totalWidth = (maxStars - 1) * starSpacing;
            float startOffset = -totalWidth * 0.5f;

            for (int i = 0; i < maxStars; i++)
            {
                if (_usingTextFallback)
                {
                    var go = new GameObject("StarTxt_"+i, typeof(RectTransform));
                    go.transform.SetParent(_canvas.transform, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
                    rt.pivot = new Vector2(0.5f,0.5f);
                    rt.localPosition = new Vector3(startOffset + i * starSpacing, 0f, 0f);
                    rt.sizeDelta = new Vector2(starScale, starScale);
                    var txt = go.AddComponent<Text>();
                    txt.text = "☆"; // empty star
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.fontSize = textFallbackSize;
                    txt.color = new Color(1f,1f,1f,0.2f);
                    txt.resizeTextForBestFit = false;
                    // Use any available font (UIFontProvider if accessible)
                    var f = UIFontProvider.Get();
                    if (f != null) txt.font = f;
                    // Wrap text fallback in an Image placeholder for unified list handling
                    var img = go.AddComponent<Image>();
                    img.enabled = false; // we control via text, keep list for iteration
                    _stars.Add(img);
                }
                else
                {
                    var s = new GameObject("Star_"+i, typeof(RectTransform));
                    s.transform.SetParent(_canvas.transform, false);
                    var rt = s.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f,0.5f);
                    rt.localPosition = new Vector3(startOffset + i * starSpacing, 0f, 0f);
                    rt.sizeDelta = new Vector2(starScale, starScale);
                    var img = s.AddComponent<Image>();
                    img.sprite = starEmpty;
                    img.color = new Color(1f, 0.85f, 0.25f, 1f);
                    if (addOutline)
                    {
                        var o = s.AddComponent<Outline>();
                        o.effectColor = new Color(0f,0f,0f,0.9f);
                        o.effectDistance = new Vector2(0.02f, -0.02f);
                    }
                    _stars.Add(img);
                }
            }
        }

        private void Refresh()
        {
            if (tower == null) return;
            int lvl = Mathf.Max(1, tower.Level);
            if (hideAtBaseLevel && lvl <= 1)
            {
                _canvas.gameObject.SetActive(false);
                return;
            }
            _canvas.gameObject.SetActive(true);
            for (int i = 0; i < _stars.Count; i++)
            {
                bool filled = i < lvl;
                var img = _stars[i];
                if (_usingTextFallback)
                {
                    var txt = img.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.text = filled ? "★" : "☆";
                        txt.color = filled ? new Color(1f,0.9f,0.4f,1f) : new Color(1f,1f,1f,0.18f);
                    }
                }
                else
                {
                    if (starFilled != null && starEmpty != null)
                        img.sprite = filled ? starFilled : starEmpty;
                    img.enabled = i < maxStars; // in case max reduced at runtime
                    img.color = filled ? new Color(1f,0.9f,0.4f,1f) : new Color(1f,1f,1f,0.15f);
                }
            }
        }
    }
}
