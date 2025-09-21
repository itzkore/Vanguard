using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Simple world-space level number displayed at the center of the tower.
    /// Black number with a subtle light outline for readability.
    /// </summary>
    [ExecuteAlways]
    public class TowerLevelNumberDisplay : MonoBehaviour
    {
        [SerializeField] private TowerBehaviour tower; // auto-assign if null
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.4f, 0f);
        [SerializeField] private int fontSize = 34;
        [SerializeField] private Color numberColor = Color.black;
        [SerializeField] private Color outlineColor = new Color(1f,1f,1f,0.85f);
        [SerializeField] private bool hideAtLevelOne = false;

        private Text _text;
        private Canvas _canvas;
        private Camera _cam;

        private void Awake()
        {
            if (tower == null) tower = GetComponent<TowerBehaviour>();
            EnsureCanvas();
            UpdateText();
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
            if (_canvas == null) return;
            if (_cam == null) _cam = Camera.main;
            transformRotation();
            _canvas.transform.position = tower != null ? tower.transform.position + localOffset : transform.position + localOffset;
        }

        private void transformRotation()
        {
            if (_cam == null) return;
            // Only rotate around Y (top-down readability)
            Vector3 dir = _canvas.transform.position - _cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                _canvas.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void OnStats(TowerBehaviour t)
        {
            UpdateText();
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("LevelNumberCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 32f;
            go.AddComponent<GraphicRaycaster>();

            var txtGO = new GameObject("Number", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var rt = txtGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
            rt.pivot = new Vector2(0.5f,0.5f);
            rt.sizeDelta = new Vector2(1f,1f);
            _text = txtGO.AddComponent<Text>();
            _text.alignment = TextAnchor.MiddleCenter;
            _text.fontSize = fontSize;
            var f = UIFontProvider.Get();
            if (f != null) _text.font = f;
            _text.color = numberColor;
            var outline = txtGO.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(0.08f,-0.08f);
        }

        private void UpdateText()
        {
            if (_text == null) return;
            int lvl = (tower != null) ? tower.Level : 1;
            if (hideAtLevelOne && lvl <= 1)
            {
                _text.text = string.Empty;
            }
            else
            {
                _text.text = lvl.ToString();
            }
        }
    }
}
