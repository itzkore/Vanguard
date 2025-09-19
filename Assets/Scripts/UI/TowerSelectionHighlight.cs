using UnityEngine;

namespace BulletHeavenFortressDefense.UI
{
    [DisallowMultipleComponent]
    public class TowerSelectionHighlight : MonoBehaviour
    {
        [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.3f, 1f);
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.15f;

        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private bool _active;
        private float _t;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }
        }

        private void Update()
        {
            if (!_active) return;
            _t += Time.unscaledDeltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(_t) * pulseIntensity;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var baseC = _baseColors[i];
                var target = new Color(
                    Mathf.Clamp01(selectedColor.r * pulse),
                    Mathf.Clamp01(selectedColor.g * pulse),
                    Mathf.Clamp01(selectedColor.b * pulse),
                    baseC.a);
                _renderers[i].color = Color.Lerp(baseC, target, 0.6f);
            }
        }

        public void SetSelected(bool on)
        {
            if (_active == on) return;
            _active = on;
            if (!on)
            {
                // restore colors
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].color = _baseColors[i];
                    }
                }
            }
            else
            {
                _t = 0f;
            }
        }
    }
}