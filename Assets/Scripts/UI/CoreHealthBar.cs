using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    public class CoreHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Image fill;
        [SerializeField] private Vector3 extraOffset = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private int sortingOrder = 100; // UI render order (World Space)
        [SerializeField, Range(0.05f, 0.5f)] private float heightRatio = 0.2f; // bar height relative to core diameter

        private Camera _cam;
        private BaseCore _core;
        private RectTransform _bgRect;
        private float _lastDiameter;

        private void Awake()
        {
            _cam = Camera.main;
            _core = BaseCore.Instance ?? FindObjectOfType<BaseCore>();
            EnsureCanvas();
        }

        private void OnEnable()
        {
            var core = BaseCore.Instance ?? _core;
            if (core != null)
            {
                core.HealthChanged += OnHealthChanged;
                OnHealthChanged(core.CurrentHealth, core.MaxHealth);
            }
        }

        private void OnDisable()
        {
            var core = BaseCore.Instance ?? _core;
            if (core != null)
            {
                core.HealthChanged -= OnHealthChanged;
            }
        }

        private void LateUpdate()
        {
            var core = BaseCore.Instance ?? _core;
            if (core == null || canvasRoot == null) return;

            // Compute core visual diameter in world units
            float diameter = GetCoreVisualDiameter(core);
            if (!Mathf.Approximately(diameter, _lastDiameter))
            {
                ResizeBar(diameter);
                _lastDiameter = diameter;
            }

            // Place just above the core
            float barHeight = Mathf.Max(0.01f, diameter * heightRatio);
            Vector3 worldOffset = new Vector3(0f, (diameter * 0.5f) + (barHeight * 0.5f), 0f) + extraOffset;
            canvasRoot.position = core.transform.position + worldOffset;
        }

        private void OnHealthChanged(int current, int max)
        {
            if (fill == null) return;
            float pct = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            fill.fillAmount = pct;
            // Color from green -> yellow -> red
            fill.color = pct >= 0.5f ? Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) / 0.5f)
                                     : Color.Lerp(Color.red, Color.yellow, pct / 0.5f);
        }

        private void EnsureCanvas()
        {
            if (canvasRoot != null && fill != null) return;

            var canvasGO = new GameObject("CoreHP_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvasRoot = canvasGO.GetComponent<RectTransform>();
            canvasRoot.localScale = Vector3.one; // world units

            var barBG = new GameObject("BarBG", typeof(Image));
            barBG.transform.SetParent(canvasRoot, false);
            var bgImg = barBG.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.45f);
            _bgRect = barBG.GetComponent<RectTransform>();
            _bgRect.sizeDelta = new Vector2(0.5f, 0.08f); // provisional, will be resized in LateUpdate
            _bgRect.pivot = new Vector2(0.5f, 0.5f);

            var barFill = new GameObject("BarFill", typeof(Image));
            barFill.transform.SetParent(barBG.transform, false);
            fill = barFill.GetComponent<Image>();
            fill.color = Color.green;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            var fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(0.01f, 0.01f);
            fillRect.offsetMax = new Vector2(-0.01f, -0.01f);
        }

        private void ResizeBar(float diameter)
        {
            if (_bgRect == null) return;
            float height = Mathf.Max(0.01f, diameter * heightRatio);
            _bgRect.sizeDelta = new Vector2(diameter, height);
        }

        private float GetCoreVisualDiameter(BaseCore core)
        {
            float diameter = 0.5f;
            var visual = core.transform.Find("CoreVisual");
            if (visual != null)
            {
                var renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || r.sprite == null) continue;
                    diameter = Mathf.Max(diameter, r.bounds.size.x);
                }
            }
            return diameter;
        }
    }
}
