using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Lightweight always-available diagnostics overlay for performance & wave metrics.
    /// Toggle with F3. Non-alloc string building using cached StringBuilder.
    /// </summary>
    public class PerformanceDiagnosticsHUD : MonoBehaviour
    {
        private static PerformanceDiagnosticsHUD _instance;
        public static PerformanceDiagnosticsHUD Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("PerformanceDiagnosticsHUD");
            _instance = go.AddComponent<PerformanceDiagnosticsHUD>();
            _instance.BuildUI();
            return _instance;
        }

        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool startVisible = false;
        [SerializeField] private int sampleWindow = 60; // frames for avg ms
        [SerializeField, Tooltip("Color at or below target FPS (green)." )] private Color goodColor = new Color(0.2f,0.95f,0.2f,1f);
        [SerializeField, Tooltip("Color for degraded performance (orange)." )] private Color warnColor = new Color(1f,0.65f,0f,1f);
        [SerializeField, Tooltip("Color for poor performance (red)." )] private Color badColor = new Color(1f,0.2f,0.2f,1f);
        [SerializeField] private int targetFps = 60;

        private Text _text;
        private Canvas _canvas;
        private float _accum;
        private int _frameCount;
        private float _avgMs;
        private const float SmoothWeight = 0.08f;
        private System.Text.StringBuilder _sb;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (_text == null) BuildUI();
            _sb = new System.Text.StringBuilder(256);
            _canvas.gameObject.SetActive(startVisible);
        }

        private void BuildUI()
        {
            _canvas = new GameObject("DiagCanvas").AddComponent<Canvas>();
            _canvas.transform.SetParent(transform, false);
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000; // above most HUD
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel").AddComponent<Image>();
            panel.transform.SetParent(_canvas.transform,false);
            var prt = panel.rectTransform; prt.anchorMin = new Vector2(0f,1f); prt.anchorMax = new Vector2(0f,1f); prt.pivot=new Vector2(0f,1f);
            prt.anchoredPosition = new Vector2(8,-8); prt.sizeDelta = new Vector2(380, 220);
            panel.color = new Color(0f,0f,0f,0.58f);

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(panel.transform,false);
            var trt = txtGO.AddComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(6,6); trt.offsetMax = new Vector2(-6,-6);
            _text = txtGO.AddComponent<Text>();
            _text.font = UIFontProvider.Get();
            _text.fontSize = 18; _text.alignment = TextAnchor.UpperLeft; _text.horizontalOverflow = HorizontalWrapMode.Overflow; _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.text = "Diagnostics";
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
            }
            if (!_canvas.gameObject.activeSelf) return;

            // FPS / frame time
            _accum += Time.unscaledDeltaTime;
            _frameCount++;
            if (_frameCount >= sampleWindow)
            {
                float ms = (_accum / _frameCount) * 1000f;
                // exponential blend
                _avgMs = Mathf.Lerp(_avgMs, ms, SmoothWeight);
                _accum = 0f; _frameCount = 0;
            }
            float fps = (_avgMs > 0f) ? 1000f / _avgMs : (1f / Time.unscaledDeltaTime);

            int activeEnemies = EnemyController.ActiveEnemies != null ? EnemyController.ActiveEnemies.Count : 0;
            int pendingBuffer = (EnemySpawnBuffer.HasInstance) ? EnemySpawnBuffer.Instance.Pending : 0;
            int enemiesRemaining = (WaveManager.HasInstance) ? WaveManager.Instance.EnemiesRemaining : 0;
            int wave = (WaveManager.HasInstance) ? WaveManager.Instance.CurrentWaveNumber : 0;
            int totalKills = (WaveManager.HasInstance) ? WaveManager.Instance.TotalKills : 0;
            int towers = 0;
            if (TowerManager.HasInstance && TowerManager.Instance.UnlockedTowers != null)
                towers = TowerManager.Instance.UnlockedTowers.Count;

            // Build text
            _sb.Length = 0;
            _sb.Append("PERF / GAME\n");
            _sb.AppendFormat("FPS: {0:F1}  Frame: {1:F2} ms\n", fps, _avgMs);
            _sb.AppendFormat("Wave: {0}  EnemiesAlive: {1}  RemainingWave: {2}\n", wave, activeEnemies, enemiesRemaining);
            _sb.AppendFormat("SpawnBufferPending: {0}\n", pendingBuffer);
            if (SpawnScheduler.HasInstance)
            {
                var (pending, tokens, maxPerSec) = SpawnScheduler.Instance.GetStats();
                float realized = SpawnScheduler.Instance.GetRealizedRatePerSecond();
                _sb.AppendFormat("SpawnSched Pending: {0} Tokens: {1:F1}/{2} Rate:{3:F1}/s\n", pending, tokens, maxPerSec, realized);
            }
            _sb.AppendFormat("Towers: {0}  Kills: {1}\n", towers, totalKills);
            _sb.AppendFormat("TimeScale: {0:F2}\n", Time.timeScale);
            // Pool stats
            _sb.Append("Pools:\n");
            if (BulletHeavenFortressDefense.Pooling.ProjectilePool.HasInstance)
            {
                var ps = BulletHeavenFortressDefense.Pooling.ProjectilePool.Instance.GetStats();
                _sb.AppendFormat("  Projectiles buckets={0} act={1} free={2}\n", ps.bucketCount, ps.totalActive, ps.totalFree);
            }
            else _sb.Append("  Projectiles: n/a\n");
            if (BulletHeavenFortressDefense.Pooling.BloodEffectPool.HasInstance)
            {
                var bs = BulletHeavenFortressDefense.Pooling.BloodEffectPool.Instance.GetStats();
                _sb.AppendFormat("  Blood act={0} free={1} spawnedF={2}\n", bs.active, bs.free, bs.spawnedThisFrame);
            }
            else _sb.Append("  Blood: n/a\n");
            _text.text = _sb.ToString();

            // Color code performance line by FPS
            if (fps >= targetFps * 0.92f) _text.color = goodColor; else if (fps >= targetFps * 0.55f) _text.color = warnColor; else _text.color = badColor;
        }
    }
}
