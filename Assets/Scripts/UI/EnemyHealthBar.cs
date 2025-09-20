using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    // Tiny world-space HP bar that follows an EnemyController
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Image fill;
        [Header("Dimensions")]
    [SerializeField, Tooltip("World-space bar width in units.")] private float width = 0.42f; // enlarged for readability
    [SerializeField, Tooltip("World-space bar height in units.")] private float height = 0.055f; // taller for clarity
    [SerializeField, Tooltip("Offset above enemy origin.")] private Vector2 worldOffset = new Vector2(0f, 0.34f); // closer
        [Header("Behavior")]
        [SerializeField] private bool smoothFill = true;
        [SerializeField, Range(1f, 30f)] private float fillLerpSpeed = 18f;
        [SerializeField] private bool hideWhenDead = true; // only hide if 0 HP
    [SerializeField] private int sortingOrder = 3;
    [SerializeField, Tooltip("Hide bar until enemy first takes damage (requested behavior).")] private bool hideUntilDamaged = true; // revert to hide until damaged
    [SerializeField, Tooltip("Force bar to remain upright and not mirror when enemy flips.")] private bool lockUpright = true;
        [Header("Colors")]
        [SerializeField] private Color colorFull = new Color(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color colorMid = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color colorLow = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color frameColor = new Color(0.9f, 0.9f, 0.9f, 0.55f);
        [SerializeField, Range(0.001f, 0.01f)] private float frameThickness = 0.0035f;

        private EnemyController _enemy;
        private RectTransform _bgRect;
        private Image _frameImage;
        private float _targetPct = 1f;
        private RectTransform _fillRect;
    private Sprite _solidSprite;
    private bool _hasTakenDamage;

        /// <summary>
        /// Called by spawner (via EnemyController) when an enemy is (re)initialized from a pool.
        /// Ensures the bar starts hidden again if configured to hideUntilDamaged and health is full.
        /// </summary>
        public void ResetForSpawn()
        {
            _hasTakenDamage = false;
            _targetPct = 1f;
            if (fill != null)
            {
                fill.fillAmount = 1f;
                if (_fillRect != null)
                {
                    _fillRect.sizeDelta = new Vector2(width, _fillRect.sizeDelta.y);
                }
                fill.color = colorFull;
            }
            if (canvasRoot != null)
            {
                // If configured to hide until damaged, start hidden; otherwise ensure visible immediately.
                canvasRoot.gameObject.SetActive(!hideUntilDamaged);
            }
        }

        private void Awake()
        {
            _enemy = GetComponentInParent<EnemyController>();
            EnsureCanvas();
            if (canvasRoot != null)
            {
                canvasRoot.gameObject.SetActive(!hideUntilDamaged);
            }
        }

        private void OnEnable()
        {
            if (_enemy != null)
            {
                _enemy.HealthChanged += OnHealthChanged;
                OnHealthChanged(_enemy.RemainingHealth, _enemy.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (_enemy != null)
            {
                _enemy.HealthChanged -= OnHealthChanged;
            }
        }

        private void LateUpdate()
        {
            if (canvasRoot == null || _enemy == null) return;
            canvasRoot.position = _enemy.transform.position + (Vector3)worldOffset;
            if (lockUpright)
            {
                // Keep rotation identity (flat) and neutralize parent flips
                canvasRoot.rotation = Quaternion.identity;
                var ls = canvasRoot.localScale;
                ls.x = Mathf.Abs(ls.x);
                ls.y = Mathf.Abs(ls.y);
                canvasRoot.localScale = ls;
            }
            if (smoothFill && fill != null)
            {
                if (!Mathf.Approximately(fill.fillAmount, _targetPct))
                {
                    fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, _targetPct, fillLerpSpeed * Time.deltaTime * 0.1f);
                    if (_fillRect != null) _fillRect.sizeDelta = new Vector2(fill.fillAmount * width, _fillRect.sizeDelta.y);
                }
                fill.color = EvaluateColor(fill.fillAmount);
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            float pct = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;
            _targetPct = pct;
            if (!_hasTakenDamage && pct < 1f)
            {
                _hasTakenDamage = true;
            }
            if (!smoothFill && fill != null)
            {
                fill.fillAmount = pct;
                if (_fillRect != null) _fillRect.sizeDelta = new Vector2(pct * width, _fillRect.sizeDelta.y);
                fill.color = EvaluateColor(pct);
            }
            if (canvasRoot != null)
            {
                bool active = true;
                if (hideWhenDead && pct <= 0f) active = false;
                if (hideUntilDamaged && !_hasTakenDamage) active = false;
                // Only update if changed to avoid unnecessary SetActive calls every health tick.
                if (canvasRoot.gameObject.activeSelf != active)
                {
                    canvasRoot.gameObject.SetActive(active);
                }
            }
        }

        private Color EvaluateColor(float pct)
        {
            if (pct >= 0.66f)
            {
                float t = Mathf.InverseLerp(1f, 0.66f, pct);
                return Color.Lerp(colorFull, colorMid, t);
            }
            else
            {
                float t = Mathf.InverseLerp(0.66f, 0f, pct);
                return Color.Lerp(colorMid, colorLow, t);
            }
        }

        private void EnsureCanvas()
        {
            if (canvasRoot != null && fill != null)
            {
                if (_bgRect != null) _bgRect.sizeDelta = new Vector2(width, height);
                return;
            }

            var canvasGO = new GameObject("EnemyHP_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvasRoot = canvasGO.GetComponent<RectTransform>();
            canvasRoot.localScale = Vector3.one;

            // Frame (slightly larger)
            var frameGO = new GameObject("Frame", typeof(Image));
            frameGO.transform.SetParent(canvasRoot, false);
            _frameImage = frameGO.GetComponent<Image>();
            _frameImage.color = frameColor;
            var frRectNew = _frameImage.rectTransform;
            frRectNew.sizeDelta = new Vector2(width + frameThickness * 2f, height + frameThickness * 2f);
            frRectNew.pivot = new Vector2(0.5f, 0.5f);

            // Background
            var bgGO = new GameObject("BG", typeof(Image));
            bgGO.transform.SetParent(frameGO.transform, false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = backgroundColor;
            _bgRect = bgImg.rectTransform;
            _bgRect.sizeDelta = new Vector2(width, height);
            _bgRect.pivot = new Vector2(0.5f, 0.5f);

            // Fill
            var fillGO = new GameObject("Fill", typeof(Image));
            fillGO.transform.SetParent(bgGO.transform, false);
            fill = fillGO.GetComponent<Image>();
            if (_solidSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            }
            fill.sprite = _solidSprite; // ensure visible pixel to fill
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = colorFull;
            _fillRect = fill.GetComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 0.5f); // width-driven for guaranteed shrink fallback
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.sizeDelta = new Vector2(width, height * 0.98f);
        }
    }
}
