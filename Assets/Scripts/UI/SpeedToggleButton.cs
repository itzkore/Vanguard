using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Simple HUD component that cycles overall gameplay speed (Time.timeScale + EnemyPace.SpeedMultiplier baseline)
    /// across predefined factors (1x, 2x, 3x). Designed for mobile quick-forward.
    /// It only affects gameplay (enemy pacing) plus global timeScale (so projectile Update etc. scale accordingly)
    /// while leaving UI animations (which use unscaled time) alone.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedToggleButton : MonoBehaviour
    {
        [Tooltip("Label Text component updated with current speed factor (e.g. 2x).")]
        [SerializeField] private Text label;
        [Tooltip("Sequence of speed factors to cycle through.")] public float[] speedSteps = new float[] { 1f, 2f, 3f };
        [Tooltip("Index we start from in speedSteps.")] public int startIndex = 0;
        [Tooltip("If true also scales EnemyPace.SpeedMultiplier (keeps relative enemy-only scaling). If false only changes Time.timeScale.")] public bool affectEnemyPace = true;
        [Tooltip("Optional base enemy pace (if <=0 uses current EnemyPace.SpeedMultiplier at Awake)." )] public float baseEnemyPace = 0f;

        private int _index;
        private float _originalTimeScale = 1f;
        private float _enemyBase;
        private Button _button;
        private bool _subscribed;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button == null)
            {
                _button = gameObject.AddComponent<Button>();
            }
            _button.onClick.AddListener(CycleSpeed);
            _originalTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            _enemyBase = baseEnemyPace > 0f ? baseEnemyPace : EnemyPace.SpeedMultiplier;
            _index = Mathf.Clamp(startIndex, 0, speedSteps.Length - 1);
            ApplyCurrent();
            TrySubscribeWaveEvents();
        }

        private void OnEnable()
        {
            TrySubscribeWaveEvents();
            // Reapply in case timescale was reset externally during disable
            ApplyCurrent();
        }

        private void OnDisable()
        {
            if (_subscribed && BulletHeavenFortressDefense.Managers.WaveManager.HasInstance)
            {
                var wm = BulletHeavenFortressDefense.Managers.WaveManager.Instance;
                wm.WaveStarted -= OnWaveStarted;
                wm.PhaseChanged -= OnPhaseChanged;
            }
            _subscribed = false;
        }

        private void TrySubscribeWaveEvents()
        {
            if (_subscribed) return;
            if (!BulletHeavenFortressDefense.Managers.WaveManager.HasInstance) return;
            var wm = BulletHeavenFortressDefense.Managers.WaveManager.Instance;
            wm.WaveStarted += OnWaveStarted;
            wm.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
        }

        private void OnWaveStarted(int wave)
        {
            // Reapply current factor to ensure Time.timeScale not reset to base
            ApplyCurrent();
        }

        private void OnPhaseChanged(BulletHeavenFortressDefense.Managers.WaveManager.WavePhase phase)
        {
            // Some transitions may restore base speed; enforce chosen multiplier
            ApplyCurrent();
        }

        private void CycleSpeed()
        {
            if (speedSteps == null || speedSteps.Length == 0) return;
            _index = (_index + 1) % speedSteps.Length;
            ApplyCurrent();
        }

        private void ApplyCurrent()
        {
            float factor = speedSteps != null && speedSteps.Length > 0 ? speedSteps[_index] : 1f;
            // Base game speed might have been altered externally; treat _originalTimeScale as baseline 1 after first use
            if (_originalTimeScale <= 0f) _originalTimeScale = 1f;
            Time.timeScale = _originalTimeScale * factor;
            if (affectEnemyPace)
            {
                EnemyPace.SpeedMultiplier = Mathf.Max(0.01f, _enemyBase * factor);
            }
            if (label != null)
            {
                label.text = factor.ToString("0") + "x";
            }
        }

        public void ResetToNormal()
        {
            _index = 0;
            ApplyCurrent();
        }
    }
}
