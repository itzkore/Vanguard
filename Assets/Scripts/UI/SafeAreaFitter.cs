using UnityEngine;
using UnityEngine.Events;

namespace BulletHeavenFortressDefense.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField, Tooltip("If true, only apply on mobile platforms at runtime.")] private bool mobileOnly = true;
        [SerializeField, Tooltip("Apply horizontal padding percent inside safe area (0-0.2 typical). 0 = none.")] [Range(0f,0.3f)] private float horizontalInsetPercent = 0f;
        [SerializeField, Tooltip("Apply vertical padding percent inside safe area (0-0.2 typical). 0 = none.")] [Range(0f,0.3f)] private float verticalInsetPercent = 0f;
        [SerializeField, Tooltip("Optional event fired after each safe area application.")] private UnityEvent onApplied;
        [SerializeField, Tooltip("Verbose logging for diagnostics.")] private bool verbose = false;

        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private RectTransform _rt;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            ApplyIfNeeded(force:true);
        }

        private void OnEnable()
        {
            ApplyIfNeeded(force:true);
        }

        private void Update()
        {
            ApplyIfNeeded();
        }

        private bool IsMobileRuntime()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return true; // allow preview in editor
#endif
            if (!Application.isMobilePlatform) return false;
            return true;
        }

        private void ApplyIfNeeded(bool force=false)
        {
            if (_rt == null) return;
            if (mobileOnly && !IsMobileRuntime()) return;

            Rect sa = Screen.safeArea;
            Vector2Int ss = new Vector2Int(Screen.width, Screen.height);
            if (!force && sa == _lastSafeArea && ss == _lastScreenSize) return;

            _lastSafeArea = sa; _lastScreenSize = ss;

            // Convert safe area rect (pixel) to normalized anchor space
            Vector2 anchorMin = new Vector2(sa.xMin / ss.x, sa.yMin / ss.y);
            Vector2 anchorMax = new Vector2(sa.xMax / ss.x, sa.yMax / ss.y);

            // Apply additional padding inside safe area
            float hInset = horizontalInsetPercent * (anchorMax.x - anchorMin.x) * 0.5f;
            float vInset = verticalInsetPercent * (anchorMax.y - anchorMin.y) * 0.5f;
            anchorMin.x += hInset; anchorMax.x -= hInset;
            anchorMin.y += vInset; anchorMax.y -= vInset;

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;

            if (verbose)
            {
                Debug.Log($"[SafeAreaFitter] Applied safe area anchors min={anchorMin} max={anchorMax} screen={ss} rawSA={sa}");
            }
            onApplied?.Invoke();
        }
    }
}
