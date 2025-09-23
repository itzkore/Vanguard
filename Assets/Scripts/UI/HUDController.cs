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
    // Wave completion announcement
    private RectTransform _waveCompleteRoot;
    private Text _waveCompleteText;
    private Coroutine _waveCompleteRoutine;

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
            AutoBindTexts();
            EnsureWaveLabelExists();
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

            TryBindWaveEvents(verbose:false);
            StartCoroutine(LateWaveRefresh());
        }

        private System.Collections.IEnumerator LateWaveRefresh()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
            if (WaveManager.HasInstance && waveText != null)
            {
                int w = WaveManager.Instance.CurrentWaveNumber;
                if (w >= 0) waveText.text = FormatWaveText(w);
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

            UnbindWaveEvents();
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

            // Late attempt to bind wave events if WaveManager appeared after HUD enabled
            if (!_waveEventsBound)
            {
                TryBindWaveEvents(verbose:false);
            }

            // Fallback: if we missed WaveCompleted event but phase is already Completed and we haven't shown text yet this wave, show it
            if (_waveEventsBound && WaveManager.HasInstance && WaveManager.Instance.CurrentPhase == WaveManager.WavePhase.Completed && !_waveCompleteShownForWave)
            {
                int w = WaveManager.Instance.CurrentWaveNumber;
                if (w > 0)
                {
                    Debug.Log("[HUDController] Detected completed phase without prior announcement, forcing wave complete UI.");
                    OnWaveCompleted(w);
                }
            }
            // Wave label self-heal: if somehow destroyed at runtime, recreate (check infrequently for cost)
            _waveLabelHealTimer += Time.unscaledDeltaTime;
            if (_waveLabelHealTimer >= 1.0f)
            {
                _waveLabelHealTimer = 0f;
                if (waveText == null)
                {
                    if (!_waveLabelLoggedMissing)
                    {
                        Debug.LogWarning("[HUDController] Wave label missing at runtime. Recreating.");
                        _waveLabelLoggedMissing = true; // only log first time to avoid spam
                    }
                    EnsureWaveLabelExists();
                    // Immediately refresh with current wave if available
                    if (WaveManager.HasInstance && waveText != null)
                    {
                        int w = WaveManager.Instance.CurrentWaveNumber;
                        waveText.text = w >= 0 ? FormatWaveText(w) : "Wave --";
                    }
                }
            }
        }

        private bool _waveEventsBound;
        private int _lastAnnouncedWave = -1;
        private bool _waveCompleteShownForWave => _lastAnnouncedWave == (WaveManager.HasInstance ? WaveManager.Instance.CurrentWaveNumber : -1);

        private void TryBindWaveEvents(bool verbose)
        {
            if (!WaveManager.HasInstance || _waveEventsBound) return;
            var wm = WaveManager.Instance;
            wm.WaveStarted += OnWaveStarted;
            wm.WaveCompleted += OnWaveCompleted;
            wm.PhaseChanged += OnPhaseChanged;
            wm.PhaseTimerUpdated += OnPhaseTimerUpdated;
            wm.WaveStarted += _ => UpdateKills();
            _waveEventsBound = true;
            if (verbose) Debug.Log("[HUDController] Wave events bound late.");

            OnPhaseChanged(wm.CurrentPhase);
            if (wm.CurrentWaveNumber > 0)
            {
                OnWaveStarted(wm.CurrentWaveNumber);
            }
            else if (waveText != null)
            {
                waveText.text = "Wave 0";
            }
            OnPhaseTimerUpdated(wm.CurrentPhase, wm.CurrentPhaseTimeRemaining);
            UpdateKills();
            StartCoroutine(LateWaveRefresh());
        }

        private void UnbindWaveEvents()
        {
            if (!_waveEventsBound || !WaveManager.HasInstance) return;
            var wm = WaveManager.Instance;
            wm.WaveStarted -= OnWaveStarted;
            wm.WaveCompleted -= OnWaveCompleted;
            wm.PhaseChanged -= OnPhaseChanged;
            wm.PhaseTimerUpdated -= OnPhaseTimerUpdated;
            _waveEventsBound = false;
        }

        private void OnWaveCompleted(int waveNumber)
        {
            _lastAnnouncedWave = waveNumber;
            Debug.Log($"[HUDController] WaveCompleted received wave={waveNumber}");
            ShowWaveCompleteAnnouncement(waveNumber);
        }

        private void ShowWaveCompleteAnnouncement(int waveNumber)
        {
            EnsureWaveCompleteUI();
            if (_waveCompleteRoutine != null)
            {
                StopCoroutine(_waveCompleteRoutine);
            }
            _waveCompleteText.text = $"You made it through wave {waveNumber}";
            _waveCompleteRoutine = StartCoroutine(WaveCompleteRoutine());
        }

        private System.Collections.IEnumerator WaveCompleteRoutine()
        {
            _waveCompleteRoot.gameObject.SetActive(true);
            CanvasGroup cg = _waveCompleteRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = _waveCompleteRoot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _waveCompleteRoot.localScale = Vector3.one * 0.85f;
            float fadeIn = 0.35f; float hold = 1.2f; float fadeOut = 0.5f;
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeIn);
                cg.alpha = k;
                _waveCompleteRoot.localScale = Vector3.one * Mathf.SmoothStep(0.85f, 1f, k);
                yield return null;
            }
            t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / fadeOut);
                cg.alpha = k;
                _waveCompleteRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, 1f-k);
                yield return null;
            }
            _waveCompleteRoot.gameObject.SetActive(false);
            _waveCompleteRoutine = null;
        }

        private void EnsureWaveCompleteUI()
        {
            if (_waveCompleteRoot != null) return;
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : this.transform;
            var go = new GameObject("WaveCompleteText", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f,0.5f);
            rt.anchorMax = new Vector2(0.5f,0.5f);
            rt.pivot = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(900f, 120f);
            _waveCompleteRoot = rt;
            var txt = go.AddComponent<Text>();
            txt.font = UIFontProvider.Get();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = Mathf.RoundToInt(HUDBootstrapper.PublishedFontBase * 0.9f);
            txt.color = new Color(1f,1f,1f,0.95f);
            txt.text = "";
            // Outline / shadow for contrast
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0f,0f,0f,0.9f); outline.effectDistance = new Vector2(2f,-2f);
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0f,0f,0f,0.5f); shadow.effectDistance = new Vector2(3f,-3f);
            _waveCompleteText = txt;
            go.SetActive(false);
        }

        private void AutoBindTexts()
        {
            // Attempt to locate Text components by common names if not wired in inspector
            if (waveText == null)
            {
                waveText = FindTextByNames("WaveText", "WaveLabel", "Wave");
            }
        }

        private Text FindTextByNames(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                var t = GetComponentInChildren<Text>(includeInactive: true);
                // Above only returns first; to refine we'd scan all children
                var all = GetComponentsInChildren<Text>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j] != null && all[j].name.Equals(n, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return all[j];
                    }
                }
            }
            return null;
        }

        private void EnsureWaveLabelExists()
        {
            if (waveText != null) return;
            // Create a simple label at top center of this canvas
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : this.transform;
            var go = new GameObject("WaveText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(200f, 40f);
            waveText = go.AddComponent<Text>();
            waveText.alignment = TextAnchor.MiddleCenter;
            waveText.font = UIFontProvider.Get();
            // Use larger font (0.65x) for visibility
            waveText.fontSize = Mathf.RoundToInt(HUDBootstrapper.PublishedFontBase * 0.65f);
            waveText.color = Color.white;
            waveText.text = "Wave --";
            // Add outline + subtle shadow for contrast
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f,0f,0f,0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f,0f,0f,0.5f);
            shadow.effectDistance = new Vector2(2f, -2f);
            _waveLabelLoggedMissing = false; // reset in case it was recreated
        }

        // --- Self-heal tracking fields ---
        private float _waveLabelHealTimer = 0f;
        private bool _waveLabelLoggedMissing = false;

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
            _toastText.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
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
                waveText.text = FormatWaveText(waveNumber);
            }
        }

        private void OnPhaseChanged(WaveManager.WavePhase phase)
        {
            if (phaseText != null)
            {
                string wavePart = WaveManager.HasInstance ? FormatWaveText(WaveManager.Instance.CurrentWaveNumber) + "  " : string.Empty;
                phaseText.text = wavePart + $"Phase: {phase.ToString().ToUpperInvariant()}";
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

            // Keep wave info fresh on timer ticks too (covers progression increments without phase change)
            if (phaseText != null && WaveManager.HasInstance)
            {
                string wavePart = FormatWaveText(WaveManager.Instance.CurrentWaveNumber) + "  ";
                string phaseLabel = phase.ToString().ToUpperInvariant();
                if (!phaseText.text.StartsWith(wavePart))
                {
                    phaseText.text = wavePart + $"Phase: {phaseLabel}";
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

        private string FormatWaveText(int current)
        {
            int total = -1;
            if (WaveManager.HasInstance)
            {
                // If multiple authored/generated waves exist, show total; else omit (infinite / virtual looping)
                var wm = WaveManager.Instance;
                var seqField = typeof(WaveManager).GetField("waveSequence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var seq = seqField != null ? seqField.GetValue(wm) : null;
                if (seq != null)
                {
                    var wavesProp = seq.GetType().GetProperty("Waves");
                    if (wavesProp != null)
                    {
                        var list = wavesProp.GetValue(seq) as System.Collections.IList;
                        if (list != null && list.Count > 1)
                        {
                            total = list.Count;
                        }
                    }
                }
            }
            return total > 0 ? $"Wave {current}/{total}" : $"Wave {current}";
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
