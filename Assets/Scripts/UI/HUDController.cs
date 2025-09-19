using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class HUDController : MonoBehaviour
    {
    private static HUDController _instance;
        [SerializeField] private Text baseHealthText;
    [SerializeField] private Text energyText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text phaseText;
        [SerializeField] private Text phaseTimerText;
    [SerializeField] private Text enemiesRemainingText;
    [SerializeField] private Text killsText;

        public Text BaseHealthText => baseHealthText;
        public Text EnergyText => energyText;
        public Text WaveText => waveText;
        public Text PhaseText => phaseText;
        public Text PhaseTimerText => phaseTimerText;
    public Text EnemiesRemainingText => enemiesRemainingText;
    public Text KillsText => killsText;

        // Simple toast notification UI
        private RectTransform _toastRoot;
        private Image _toastBackground;
        private Text _toastText;
        private Coroutine _toastRoutine;

        public static HUDController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<HUDController>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void OnEnable()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                BaseCore.Instance.HealthChanged += OnBaseHealthChanged;
                OnBaseHealthChanged(BaseCore.Instance.CurrentHealth, BaseCore.Instance.MaxHealth);
            }

            if (EconomySystem.HasInstance && energyText != null)
            {
                EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.WaveStarted += OnWaveStarted;
                WaveManager.Instance.PhaseChanged += OnPhaseChanged;
                WaveManager.Instance.PhaseTimerUpdated += OnPhaseTimerUpdated;
                WaveManager.Instance.WaveStarted += _ => UpdateKills();
                OnPhaseChanged(WaveManager.Instance.CurrentPhase);
                if (WaveManager.Instance.CurrentWaveNumber > 0)
                {
                    OnWaveStarted(WaveManager.Instance.CurrentWaveNumber);
                }
                else if (waveText != null)
                {
                    waveText.text = "Wave 0";
                }

                OnPhaseTimerUpdated(WaveManager.Instance.CurrentPhase, WaveManager.Instance.CurrentPhaseTimeRemaining);
                UpdateKills();
            }
        }

        private void OnDisable()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                BaseCore.Instance.HealthChanged -= OnBaseHealthChanged;
            }

            if (EconomySystem.HasInstance && energyText != null)
            {
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
            }

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.WaveStarted -= OnWaveStarted;
                WaveManager.Instance.PhaseChanged -= OnPhaseChanged;
                WaveManager.Instance.PhaseTimerUpdated -= OnPhaseTimerUpdated;
            }
        }

        private bool _coreSubscriptionVerified;

        private void Update()
        {
            // Safety net: if HUD enabled before BaseCore spawned, subscribe once it exists.
            if (!_coreSubscriptionVerified)
            {
                if (BaseCore.Instance != null && baseHealthText != null)
                {
                    // Remove just in case, then add (prevents duplicate if race occurred)
                    BaseCore.Instance.HealthChanged -= OnBaseHealthChanged;
                    BaseCore.Instance.HealthChanged += OnBaseHealthChanged;
                    OnBaseHealthChanged(BaseCore.Instance.CurrentHealth, BaseCore.Instance.MaxHealth);
                    _coreSubscriptionVerified = true;
                }
            }
        }

        public static void Toast(string message, float duration = 2.5f)
        {
            if (Instance != null)
            {
                Instance.ShowToast(message, duration);
            }
        }

        public void ShowToast(string message, float duration = 2.5f)
        {
            if (string.IsNullOrEmpty(message)) return;
            EnsureToastUI();
            _toastText.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(duration));
        }

        private System.Collections.IEnumerator ToastRoutine(float duration)
        {
            float fadeIn = 0.2f;
            float fadeOut = 0.3f;
            float hold = Mathf.Max(0f, duration - fadeIn - fadeOut);

            SetToastAlpha(0f);
            _toastRoot.gameObject.SetActive(true);

            // Fade in
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / fadeIn);
                SetToastAlpha(a);
                yield return null;
            }

            // Hold
            t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / fadeOut);
                SetToastAlpha(a);
                yield return null;
            }

            _toastRoot.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private void EnsureToastUI()
        {
            if (_toastRoot != null) return;
            // Create a simple panel at the top-center of HUD canvas
            var root = this.transform as RectTransform;
            if (root == null)
            {
                var canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    root = canvas.transform as RectTransform;
                }
            }

            var go = new GameObject("ToastPanel", typeof(RectTransform));
            go.transform.SetParent(root, false);
            _toastRoot = go.GetComponent<RectTransform>();
            _toastRoot.anchorMin = new Vector2(0.5f, 1f);
            _toastRoot.anchorMax = new Vector2(0.5f, 1f);
            _toastRoot.pivot = new Vector2(0.5f, 1f);
            _toastRoot.anchoredPosition = new Vector2(0f, -36f);
            _toastRoot.sizeDelta = new Vector2(520f, 34f);

            _toastBackground = go.AddComponent<Image>();
            _toastBackground.color = new Color(0f, 0f, 0f, 0.75f);

            var textGO = new GameObject("ToastText", typeof(RectTransform));
            textGO.transform.SetParent(_toastRoot, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10f, 6f);
            textRT.offsetMax = new Vector2(-10f, -6f);

            _toastText = textGO.AddComponent<Text>();
            _toastText.text = string.Empty;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _toastText.resizeTextForBestFit = true;
            _toastText.resizeTextMinSize = 10;
            _toastText.resizeTextMaxSize = 24;

            SetToastAlpha(0f);
            _toastRoot.gameObject.SetActive(false);
        }

        private void SetToastAlpha(float a)
        {
            if (_toastBackground != null)
            {
                var c = _toastBackground.color; c.a = 0.75f * a; _toastBackground.color = c;
            }
            if (_toastText != null)
            {
                var c2 = _toastText.color; c2.a = a; _toastText.color = c2;
            }
        }

        public void Configure(Text baseLabel, Text energyLabel, Text waveLabel, Text phaseLabel, Text timerLabel, Text enemiesLabel, Text killsLabel = null)
        {
            baseHealthText = baseLabel;
            energyText = energyLabel;
            waveText = waveLabel;
            phaseText = phaseLabel;
            phaseTimerText = timerLabel;
            enemiesRemainingText = enemiesLabel;
            killsText = killsLabel;
            ApplyInitialValues();
        }

        private void ApplyInitialValues()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                OnBaseHealthChanged(BaseCore.Instance.CurrentHealth, BaseCore.Instance.MaxHealth);
            }

            if (EconomySystem.HasInstance && energyText != null)
            {
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }

            if (WaveManager.HasInstance)
            {
                if (WaveManager.Instance.CurrentWaveNumber > 0)
                {
                    OnWaveStarted(WaveManager.Instance.CurrentWaveNumber);
                }
                else if (waveText != null)
                {
                    waveText.text = "Wave 0";
                }

                OnPhaseChanged(WaveManager.Instance.CurrentPhase);
                OnPhaseTimerUpdated(WaveManager.Instance.CurrentPhase, WaveManager.Instance.CurrentPhaseTimeRemaining);
                UpdateKills();
            }
            else
            {
                if (phaseText != null)
                {
                    phaseText.text = "Phase: --";
                }

                if (phaseTimerText != null)
                {
                    phaseTimerText.text = "--";
                }

                if (enemiesRemainingText != null)
                {
                    enemiesRemainingText.text = "Enemies: 0";
                }
                if (killsText != null)
                {
                    killsText.text = "Kills: 0";
                }
            }
        }

        private void OnBaseHealthChanged(int current, int max)
        {
            if (baseHealthText != null)
            {
                float pct = max > 0 ? (float)current / max : 0f;
                // Rich text with gradient-esque emphasis (fallback if custom font lacks rich tags: it will just show plain text)
                string label = "<b>Core Health</b>";
                string value = $"{current}/{max}";
                // Color shift: green -> yellow -> red
                Color c = Color.Lerp(new Color(0.85f,0.15f,0.15f), Color.Lerp(new Color(0.95f,0.75f,0.15f), new Color(0.2f,0.9f,0.35f), Mathf.Clamp01(pct*2f)), Mathf.Clamp01(pct*2f));
                string hex = ColorUtility.ToHtmlStringRGB(c);
                baseHealthText.supportRichText = true;
                baseHealthText.text = $"{label}: <color=#{hex}>{value}</color>";
            }
        }

        private void OnEnergyChanged(int value)
        {
            if (energyText != null)
            {
                energyText.text = $"€ {value}";
            }
        }

        private void OnWaveStarted(int waveNumber)
        {
            if (waveText != null)
            {
                waveText.text = $"Wave {waveNumber}";
            }
        }

        private void OnPhaseChanged(WaveManager.WavePhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = $"Phase: {phase.ToString().ToUpperInvariant()}";
            }

            if (phaseTimerText != null)
            {
                phaseTimerText.text = phase == WaveManager.WavePhase.Combat ? "--" : FormatTimer(WaveManager.Instance?.CurrentPhaseTimeRemaining ?? 0f);
            }

            UpdateEnemiesRemaining();
        }

        private void OnPhaseTimerUpdated(WaveManager.WavePhase phase, float timeRemaining)
        {
            if (phaseTimerText != null)
            {
                if (timeRemaining < 0f || phase == WaveManager.WavePhase.Combat)
                {
                    phaseTimerText.text = "--";
                }
                else
                {
                    phaseTimerText.text = FormatTimer(Mathf.Max(0f, timeRemaining));
                }
            }

            UpdateEnemiesRemaining();
        }

        private void UpdateEnemiesRemaining()
        {
            if (enemiesRemainingText != null && WaveManager.HasInstance)
            {
                enemiesRemainingText.text = $"Enemies: {WaveManager.Instance.EnemiesRemaining}";
            }
            UpdateKills();
        }

        private void UpdateKills()
        {
            if (killsText != null && WaveManager.HasInstance)
            {
                killsText.text = $"Kills: {WaveManager.Instance.TotalKills}";
            }
        }

        private string FormatTimer(float seconds)
        {
            int wholeSeconds = Mathf.CeilToInt(seconds);
            int mins = wholeSeconds / 60;
            int secs = wholeSeconds % 60;
            return mins > 0 ? $"{mins:00}:{secs:00}" : $"{secs:00}";
        }
    }
}
