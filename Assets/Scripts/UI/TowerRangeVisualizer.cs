using UnityEngine;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Renders a simple circle (LineRenderer) to show a tower's effective attack range while selected.
    /// </summary>
    [DisallowMultipleComponent]
    public class TowerRangeVisualizer : MonoBehaviour
    {
        [SerializeField, Tooltip("Number of line segments used to approximate the circle.")] private int segments = 60;
        [SerializeField, Tooltip("Line color for the range circle.")] private Color lineColor = new Color(0.2f, 0.9f, 1f, 0.35f);
        [SerializeField, Tooltip("Line width in world units.")] private float lineWidth = 0.05f;
        [SerializeField, Tooltip("If true, will refresh the radius every frame from TowerBehaviour.CurrentRange (handles dynamic buffs).")]
        private bool liveUpdate = false;

        private LineRenderer _lr;
        private float _radius;
        private Entities.TowerBehaviour _tower;
        private bool _visible;

        private void Awake()
        {
            _tower = GetComponent<Entities.TowerBehaviour>();
            EnsureLine();
            ApplyStyle();
            HideImmediate();
        }

        private void EnsureLine()
        {
            if (_lr != null) return;
            _lr = GetComponent<LineRenderer>();
            if (_lr == null)
            {
                _lr = gameObject.AddComponent<LineRenderer>();
            }
            _lr.useWorldSpace = false; // draw in local space so it follows tower automatically
            _lr.loop = true;
            _lr.positionCount = Mathf.Max(segments, 3);
            _lr.sortingOrder = 1000; // render above most sprites
            // Provide a very lightweight material (Sprites/Default) so line is visible
            if (_lr.material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _lr.material = new Material(shader);
                }
            }
        }

        private void ApplyStyle()
        {
            if (_lr == null) return;
            _lr.startWidth = lineWidth;
            _lr.endWidth = lineWidth;
            _lr.startColor = lineColor;
            _lr.endColor = lineColor;
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            if (_lr != null)
            {
                _lr.enabled = on; // toggle renderer
            }
            if (on)
            {
                // Refresh radius when shown
                if (_tower != null)
                {
                    UpdateRadius(_tower.CurrentRange);
                }
            }
        }

        public void UpdateRadius(float radius)
        {
            _radius = Mathf.Max(0f, radius);
            if (!_visible || _lr == null) return;
            var count = Mathf.Max(segments, 3);
            if (_lr.positionCount != count)
            {
                _lr.positionCount = count;
            }
            float step = (Mathf.PI * 2f) / (count);
            for (int i = 0; i < count; i++)
            {
                float a = step * i;
                float x = Mathf.Cos(a) * _radius;
                float y = Mathf.Sin(a) * _radius;
                _lr.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            if (liveUpdate && _tower != null)
            {
                if (!Mathf.Approximately(_tower.CurrentRange, _radius))
                {
                    UpdateRadius(_tower.CurrentRange);
                }
            }
        }

        private void HideImmediate()
        {
            if (_lr != null) _lr.enabled = false;
            _visible = false;
        }
    }
}
