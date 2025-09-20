using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.Managers
{
    public class WaveManager : Singleton<WaveManager>
    {
        public enum WavePhase
        {
            Idle,
            Shop,
            Preparation,
            Combat,
            Completed
        }

        [SerializeField] private WaveSequence waveSequence;
        [SerializeField, Tooltip("Seconds to wait after a wave fully resolves before starting the next.")] private float interWaveDelay = 4f;
        [SerializeField, Tooltip("Loop back to the first wave after finishing the last one.")] private bool loopSequence = true;
        [Header("Events")]
        [SerializeField] private GameEvent onPhaseChanged;
        [SerializeField] private GameEvent onWaveStarted;
    [SerializeField] private GameEvent onWaveCompleted;
    [Header("Auto Generation / Balance")]
    [SerializeField, Tooltip("If true and sequence empty, generate procedural waves.")] private bool autoGenerateSequence = true;
    [SerializeField, Tooltip("Enemy prototype used for auto-generated waves.")] private EnemyData defaultEnemyData;
    [SerializeField, Tooltip("Target number of waves to auto-generate.")] private int targetAutoWaves = 100;
    [SerializeField, Tooltip("Shop seconds each wave (auto-gen). ")] private float autoShopSeconds = 12f;
    [SerializeField, Tooltip("Prep seconds each wave (auto-gen). ")] private float autoPrepSeconds = 5f;
    [SerializeField, Tooltip("Delay after combat (auto-gen). ")] private float autoPostSeconds = 4f;
    [SerializeField, Tooltip("If true, will extend (top-up) an existing wave sequence up to targetAutoWaves using procedural generation instead of only when empty.")] private bool extendExistingWithAuto = true;
    [SerializeField, Tooltip("Enable extra debug logs for wave generation & spawning.")] private bool verboseLogging = false;
    [SerializeField, Tooltip("Automatically destroy duplicate WaveManager instances at runtime, keeping the first one.")] private bool autoDestroyExtras = true;
    [Header("Spawn Position Adjustments")] 
    [SerializeField, Tooltip("Additional world X offset added when spawning along the right edge (pushes enemies further to the right). Positive values move spawn further away.")] private float rightEdgeExtraOffset = 4.0f;
    [SerializeField, Tooltip("Clamp so enemies never spawn with X less than this (prevents appearing inside the fortress area).")] private float minEnemySpawnX = -9999f;
    [SerializeField, Tooltip("If true, ignore horizontal left-shift spacing so all right-edge spawns appear in a vertical column.")] private bool forceRightEdgeColumn = true;
    [SerializeField, Tooltip("Max random inward (left) inset when forcing a column, to avoid perfect overlap. 0 = none.")] private float rightEdgeRandomInsetMax = 0.2f;
    [Header("Performance / Progressive Spawning")]
    [SerializeField, Tooltip("Enable throttling of very large spawn bursts to avoid single-frame hitching (notably > ~100 enemies)")] private bool progressiveLargeWaveSpawning = true;
    [SerializeField, Tooltip("If an individual spawn entry (count) >= this threshold AND progressive spawning enabled, it will be spread across multiple frames.")] private int progressiveSpawnThreshold = 60;
    [SerializeField, Tooltip("Maximum enemies spawned in a single frame when throttling a large entry.")] private int maxEnemySpawnsPerFrame = 20;
    [SerializeField, Tooltip("Optional safety cap: even during throttling if spawnInterval > 0 it already yields; so we only throttle zero-interval bursts.")] private bool throttleOnlyWhenNoInterval = true;
    [SerializeField, Tooltip("Verbose logs for progressive throttling decisions.")] private bool verboseProgressiveLogs = false;

        private readonly HashSet<int> _activeWaveEnemyIds = new();

        private int _currentWaveIndex = -1;
    // Virtual counter used ONLY when there's effectively one real wave asset; increments each iteration for scaling.
    private int _virtualSingleWaveCounter = 0;
        private Coroutine _loopRoutine;
        private WavePhase _currentPhase = WavePhase.Idle;
        private WaveData _activeWave;
        private bool _advanceRequested;
        private float _phaseTimer;
        private bool _spawningWave;
    private int _totalKills;
        private float _rapidBaseDamage;

        public WavePhase CurrentPhase => _currentPhase;
        // Public wave number shown to player: if we have multiple authored/generated waves, just index+1.
        // If only one wave exists, show the virtual counter instead (starts at 1).
        public int CurrentWaveNumber => (waveSequence != null && waveSequence.Waves.Count > 1)
            ? (_currentWaveIndex + 1)
            : Mathf.Max(1, _virtualSingleWaveCounter);
        public WaveData ActiveWave => _activeWave;
        public float CurrentPhaseTimeRemaining => _phaseTimer;
        public bool IsRunning => _loopRoutine != null;
        public bool CanAdvancePhase => _currentPhase == WavePhase.Shop || _currentPhase == WavePhase.Preparation;
        public int EnemiesRemaining => _activeWaveEnemyIds.Count;
    public bool IsSpawningWave => _spawningWave;
        public int TotalKills => _totalKills;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCompleted;
        public event Action<WavePhase> PhaseChanged;
        public event Action<WavePhase, float> PhaseTimerUpdated;

        private void OnEnable()
        {
            DetectDuplicateInstances();
            EnemyController.EnemyDefeated += HandleEnemyDefeated;
        }

        private void OnDisable()
        {
            EnemyController.EnemyDefeated -= HandleEnemyDefeated;
        }

        private void DetectDuplicateInstances()
        {
            var all = FindObjectsOfType<WaveManager>(true);
            if (all.Length > 1)
            {
                string list = string.Empty;
                foreach (var wm in all)
                {
                    list += $"\n - {wm.gameObject.name} (activeInHierarchy={wm.gameObject.activeInHierarchy}, scene={wm.gameObject.scene.name})";
                }
                Debug.LogWarning($"[WaveManager] Detected {all.Length} instances!{list}");

                if (autoDestroyExtras)
                {
                    // Keep the first (this) and destroy all others
                    for (int i = 0; i < all.Length; i++)
                    {
                        var inst = all[i];
                        if (inst != this)
                        {
                            Debug.LogWarning($"[WaveManager] Destroying duplicate instance: {inst.gameObject.name}", inst);
                            Destroy(inst.gameObject);
                        }
                    }
                }
            }
        }

        public void StartSequence()
        {
            if (_loopRoutine != null)
            {
                Debug.LogWarning("[WaveManager] StartSequence called while already running – IGNORING to prevent wave counter reset.");
                return; // Guard against double-starts resetting progression
            }
            if (waveSequence == null)
            {
                waveSequence = ScriptableObject.CreateInstance<WaveSequence>();
            }

            // Defensive: if defaultEnemyData reference was lost (e.g., due to asset rename / serialization issue), attempt to auto-find one.
            if (defaultEnemyData == null)
            {
                TryFindDefaultEnemyDataViaResources();
            }

            // Remove any null wave entries (can happen if ScriptableObjects were deleted or broken in the asset list)
            PruneNullWaves();

            // Auto-generate if empty OR (optionally) top-up existing list
            if (autoGenerateSequence)
            {
                if (defaultEnemyData == null)
                {
                    if (waveSequence.Waves.Count <= 1)
                    {
                        // Try to extend using first wave's first spawn as template if possible
                        TryAutoExtendUsingFirstWave();
                    }
                    else
                    {
                        Debug.LogWarning("WaveManager: autoGenerateSequence enabled but defaultEnemyData missing and cannot extend (multiple waves already).", this);
                    }
                }
                else
                {
                    if (waveSequence.Waves.Count == 0)
                    {
                        if (verboseLogging) Debug.Log("[WaveManager] Generating full procedural wave list.");
                        GenerateAutoWaves();
                    }
                    else if (extendExistingWithAuto && waveSequence.Waves.Count < targetAutoWaves)
                    {
                        if (verboseLogging) Debug.Log($"[WaveManager] Extending existing wave list ({waveSequence.Waves.Count}) up to {targetAutoWaves}.");
                        for (int wave = waveSequence.Waves.Count + 1; wave <= targetAutoWaves; wave++)
                        {
                            GenerateSingleAutoWave(wave);
                        }
                    }
                }
            }

            if (waveSequence.Waves.Count == 0)
            {
                Debug.LogWarning("WaveManager: No waves available to start (auto-generation may have failed).", this);
                return;
            }

            // Ensure first wave meets baseline spec (e.g., 30 enemies) even if an old asset had fewer (like 5)
            NormalizeFirstWave();
            // If defaultEnemyData is missing try to grab it from first wave spawn so later adjustments & fills work
            AutoAssignDefaultEnemyDataIfMissing();
            // If later waves exist but are empty (no spawns) – populate them using scaling & a source enemy so we don't fast-skip them
            EnsureNonEmptyWaves();
            // Ensure all subsequent waves follow scaling formula even if pre-authored asset has flat counts
            AdjustWaveEnemyCounts();
            // If after pruning we only have 1 wave, try to extend procedurally using its first spawn as template
            if (autoGenerateSequence && waveSequence.Waves.Count == 1)
            {
                TryAutoExtendUsingFirstWave();
            }
            // If only one wave: keep looping; we'll use _virtualSingleWaveCounter to show progression & scale enemy counts.
            if (waveSequence.Waves.Count <= 1 && verboseLogging)
            {
                Debug.Log("[WaveManager] Single-wave mode: looping enabled, virtual counter will drive scaling.");
            }

            Debug.Log("[WaveManager] StartSequence invoked – resetting internal state.");
            StopSequence();
            _totalKills = 0;
            DetermineRapidTowerBaseDamage();
            _loopRoutine = StartCoroutine(RunSequence());
        }

        // Attempts to locate any EnemyData asset in Resources (or loaded assets) to serve as default when the serialized field is null.
        // This prevents a silent zero-enemy situation when references were lost; prefers an asset whose prefab is non-null.
        private void TryFindDefaultEnemyDataViaResources()
        {
            if (defaultEnemyData != null) return;
            // Broad load: empty path loads all in Resources. (Safe if project keeps limited EnemyData assets there.)
            var all = Resources.LoadAll<EnemyData>(string.Empty);
            EnemyData candidate = null;
            if (all != null && all.Length > 0)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var ed = all[i];
                    if (ed == null) continue;
                    if (ed.Prefab != null) { candidate = ed; break; }
                    if (candidate == null) candidate = ed; // fallback any
                }
            }
            if (candidate != null)
            {
                defaultEnemyData = candidate;
                Debug.Log("[WaveManager] Auto-assigned defaultEnemyData via Resources fallback: " + candidate.name, this);
            }
            else if (verboseLogging)
            {
                Debug.LogWarning("[WaveManager] Resources fallback could not find any EnemyData asset.", this);
            }
        }

        private void NormalizeFirstWave()
        {
            if (waveSequence == null || waveSequence.Waves.Count == 0) return;
            var first = waveSequence.Waves[0];
            if (first == null) return;
            int current = first.TotalEnemyCount;
            int desired = BulletHeavenFortressDefense.Balance.BalanceConfig.FirstWaveEnemyCount;
            if (current >= desired) {
                if (verboseLogging) Debug.Log($"[WaveManager] First wave already ok count={current}.");
                return;
            }
            // Determine enemyData to use: prefer defaultEnemyData, else reuse the first existing spawn's enemyData.
            EnemyData source = defaultEnemyData;
            if (source == null)
            {
                // try first entry
                if (first.Spawns != null && first.Spawns.Count > 0)
                {
                    source = first.Spawns[0].enemyData;
                }
            }
            if (source == null)
            {
                Debug.LogWarning("[WaveManager] Cannot normalize first wave (no defaultEnemyData and no existing enemyData).", this);
                return;
            }
            Debug.Log($"[WaveManager] FORCE normalizing first wave from {current} -> {desired} enemies using {(source == defaultEnemyData ? "defaultEnemyData" : "existing first spawn data") }.");
            first.ClearSpawns();
            var entry = new Data.WaveSpawnEntry
            {
                enemyData = source,
                count = desired,
                spawnInterval = 0.35f,
                spawnPointId = -1,
                spawnAlongRightEdge = true
            };
            first.AddSpawnEntry(entry);
        }

        private void AdjustWaveEnemyCounts()
        {
            if (waveSequence == null) return;
            // Start from wave 2 (index 1)
            for (int i = 1; i < waveSequence.Waves.Count; i++)
            {
                var wd = waveSequence.Waves[i];
                if (wd == null) continue;
                int expected = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyCountForWave(i + 1);
                int current = wd.TotalEnemyCount;
                if (current == expected) continue;
                // If there is at least one spawn entry, adjust first; else create a new one using defaultEnemyData
                EnemyData source = defaultEnemyData;
                if (source == null && wd.Spawns != null && wd.Spawns.Count > 0)
                {
                    source = wd.Spawns[0].enemyData;
                }
                if (source == null) continue; // can't adjust without a source
                if (wd.Spawns != null && wd.Spawns.Count > 0)
                {
                    // Replace first entry with expected count (keep its interval & flags)
                    var first = wd.Spawns[0];
                    first.count = expected;
                    wd.ClearSpawns();
                    wd.AddSpawnEntry(first);
                }
                else
                {
                    var entry = new BulletHeavenFortressDefense.Data.WaveSpawnEntry
                    {
                        enemyData = source,
                        count = expected,
                        spawnInterval = expected <= 40 ? 0.4f : 0.25f,
                        spawnPointId = -1,
                        spawnAlongRightEdge = true
                    };
                    wd.ClearSpawns();
                    wd.AddSpawnEntry(entry);
                }
                if (verboseLogging) Debug.Log($"[WaveManager] Adjusted wave {i+1} enemy count {current} -> {expected}.");
            }
        }

        private void AutoAssignDefaultEnemyDataIfMissing()
        {
            if (defaultEnemyData != null) return;
            if (waveSequence == null || waveSequence.Waves.Count == 0) return;
            var first = waveSequence.Waves[0];
            if (first != null && first.Spawns != null && first.Spawns.Count > 0)
            {
                var candidate = first.Spawns[0].enemyData;
                if (candidate != null)
                {
                    defaultEnemyData = candidate;
                    if (verboseLogging) Debug.Log("[WaveManager] Auto-assigned defaultEnemyData from first wave spawn.");
                }
            }
        }

        private void EnsureNonEmptyWaves()
        {
            if (waveSequence == null) return;
            if (waveSequence.Waves.Count <= 1) return;
            EnemyData source = defaultEnemyData;
            // Fallback: if still null, attempt from first non-null spawn among waves
            if (source == null)
            {
                for (int i = 0; i < waveSequence.Waves.Count && source == null; i++)
                {
                    var w = waveSequence.Waves[i];
                    if (w?.Spawns != null && w.Spawns.Count > 0)
                        source = w.Spawns[0].enemyData;
                }
            }
            if (source == null)
            {
                if (verboseLogging) Debug.LogWarning("[WaveManager] EnsureNonEmptyWaves: no source enemy found; cannot fill empty waves.");
                return;
            }
            for (int i = 1; i < waveSequence.Waves.Count; i++)
            {
                var wd = waveSequence.Waves[i];
                if (wd == null) continue;
                bool empty = wd.Spawns == null || wd.Spawns.Count == 0 || wd.TotalEnemyCount == 0;
                if (!empty) continue;
                int expected = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyCountForWave(i + 1);
                var entry = new BulletHeavenFortressDefense.Data.WaveSpawnEntry
                {
                    enemyData = source,
                    count = expected,
                    spawnInterval = expected <= 40 ? 0.4f : 0.25f,
                    spawnPointId = -1,
                    spawnAlongRightEdge = true
                };
                wd.ClearSpawns();
                wd.AddSpawnEntry(entry);
                if (verboseLogging) Debug.Log($"[WaveManager] Filled empty wave {i+1} with {expected} enemies (fallback generation).");
            }
        }

        private void PruneNullWaves()
        {
            if (waveSequence == null) return;
            int removed = 0;
            for (int i = waveSequence.Waves.Count - 1; i >= 0; i--)
            {
                if (waveSequence.Waves[i] == null)
                {
                    waveSequence.Waves.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
            {
                Debug.LogWarning($"[WaveManager] Pruned {removed} null wave entries. Remaining={waveSequence.Waves.Count}");
            }
        }

        private void TryAutoExtendUsingFirstWave()
        {
            if (waveSequence == null) return;
            if (waveSequence.Waves.Count == 0) return;
            var first = waveSequence.Waves[0];
            if (first == null || first.Spawns == null || first.Spawns.Count == 0) return;
            var template = first.Spawns[0];
            var baseEnemy = template.enemyData;
            if (baseEnemy == null) return;
            // Generate additional waves up to a modest count (e.g., 10) using scaling formula
            int target = Mathf.Max(5, targetAutoWaves);
            for (int w = waveSequence.Waves.Count + 1; w <= target; w++)
            {
                int count = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyCountForWave(w);
                var wd = ScriptableObject.CreateInstance<WaveData>();
                wd.ConfigurePhaseDurations(autoShopSeconds, autoPrepSeconds, autoPostSeconds);
                wd.ClearSpawns();
                wd.AddSpawnEntry(new BulletHeavenFortressDefense.Data.WaveSpawnEntry
                {
                    enemyData = baseEnemy,
                    count = count,
                    spawnInterval = count <= 40 ? 0.4f : 0.25f,
                    spawnPointId = -1,
                    spawnAlongRightEdge = true
                });
                waveSequence.Waves.Add(wd);
            }
            if (verboseLogging) Debug.Log($"[WaveManager] Auto-extended waves using first wave template up to {waveSequence.Waves.Count} waves (fallback generation).");
        }

        public void StopSequence()
        {
            Debug.Log("[WaveManager] StopSequence invoked – clearing state.");
            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
                _loopRoutine = null;
            }

            _currentWaveIndex = -1;
            _virtualSingleWaveCounter = 0;
            _activeWaveEnemyIds.Clear();
            _activeWave = null;
            _phaseTimer = 0f;
            _advanceRequested = false;
            _spawningWave = false;
            _totalKills = 0;
            SetPhase(WavePhase.Idle);
        }

        public void RequestAdvancePhase()
        {
            if (!CanAdvancePhase)
            {
                return;
            }

            _advanceRequested = true;
        }

        private IEnumerator RunSequence()
        {
            while (waveSequence != null && waveSequence.Waves.Count > 0)
            {
                _currentWaveIndex++;
                if (_currentWaveIndex >= waveSequence.Waves.Count)
                {
                    if (autoGenerateSequence && waveSequence.Waves.Count < targetAutoWaves && defaultEnemyData != null)
                    {
                        GenerateSingleAutoWave(waveSequence.Waves.Count + 1);
                    }
                    if (_currentWaveIndex >= waveSequence.Waves.Count)
                    {
                        if (!loopSequence)
                        {
                            break;
                        }
                        // Single-wave loop: keep index at 0 (only element) and just proceed; scaling uses virtual counter.
                        if (waveSequence.Waves.Count <= 1)
                        {
                            _currentWaveIndex = 0;
                        }
                        else
                        {
                            _currentWaveIndex = 0; // multi-wave wrap – but we keep numbering simple (starts over at 1)
                        }
                    }
                }

                // SINGLE-WAVE MODE: increment virtual counter BEFORE we derive scaling and assign active wave so scaling affects this iteration.
                if (waveSequence.Waves.Count <= 1)
                {
                    _virtualSingleWaveCounter++;
                    if (verboseLogging) Debug.Log($"[WaveManager] Single-wave iteration start -> virtualWave={_virtualSingleWaveCounter}");
                }

                _activeWave = waveSequence.Waves[_currentWaveIndex];
                if (_activeWave == null)
                {
                    Debug.LogWarning($"WaveManager: Wave index {_currentWaveIndex} is null.", this);
                    continue;
                }

                // Determine effective wave number for scaling: if single wave, use virtual counter (increment below) else CurrentWaveNumber
                int scalingWaveNumber = (waveSequence != null && waveSequence.Waves.Count > 1)
                    ? (_currentWaveIndex + 1)
                    : Mathf.Max(1, _virtualSingleWaveCounter);
                // Runtime safety: enforce expected enemy count for this wave (in case asset changed after StartSequence adjustments)
                int expectedCount = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyCountForWave(scalingWaveNumber);
                int currentCount = _activeWave.TotalEnemyCount;
                if (currentCount != expectedCount && expectedCount > 0)
                {
                    EnemyData src = defaultEnemyData;
                    if (src == null && _activeWave.Spawns != null && _activeWave.Spawns.Count > 0)
                    {
                        src = _activeWave.Spawns[0].enemyData;
                    }
                    if (src != null)
                    {
                        var entry = new BulletHeavenFortressDefense.Data.WaveSpawnEntry
                        {
                            enemyData = src,
                            count = expectedCount,
                            spawnInterval = expectedCount <= 40 ? 0.4f : 0.25f,
                            spawnPointId = -1,
                            spawnAlongRightEdge = true
                        };
                        if (_activeWave is BulletHeavenFortressDefense.Data.WaveData wdata)
                        {
                            wdata.ClearSpawns();
                            wdata.AddSpawnEntry(entry);
                        }
                        if (verboseLogging) Debug.Log($"[WaveManager] Runtime adjusted Wave {CurrentWaveNumber} count {currentCount} -> {expectedCount} (virtual scaling).");
                    }
                }

                yield return RunShopPhase();
                yield return RunPreparationPhase();
                yield return RunCombatPhase();

                SetPhase(WavePhase.Completed);
                onWaveCompleted?.Raise();
                WaveCompleted?.Invoke(CurrentWaveNumber);

                float delay = Mathf.Max(_activeWave.PostCombatDelay, interWaveDelay);
                if (delay > 0f)
                {
                    yield return WaitForDelay(delay);
                }
            }

            SetPhase(WavePhase.Completed);
        }

        private IEnumerator RunShopPhase()
        {
            if (verboseLogging) Debug.Log($"[WaveManager] Enter Shop Phase wave={CurrentWaveNumber}");
            SetPhase(WavePhase.Shop);
            yield return RunPhaseTimer(_activeWave.ShopDuration, _activeWave.RequiresManualShopAdvance);
        }

        private IEnumerator RunPreparationPhase()
        {
            if (verboseLogging) Debug.Log($"[WaveManager] Enter Prep Phase wave={CurrentWaveNumber}");
            SetPhase(WavePhase.Preparation);
            yield return RunPhaseTimer(_activeWave.PreparationDuration, _activeWave.RequiresManualPrepAdvance);
        }

        private IEnumerator RunCombatPhase()
        {
            if (verboseLogging) Debug.Log($"[WaveManager] Enter Combat Phase wave={CurrentWaveNumber}");
            SetPhase(WavePhase.Combat);
            onWaveStarted?.Raise();
            WaveStarted?.Invoke(CurrentWaveNumber);

            _advanceRequested = false;
            _activeWaveEnemyIds.Clear();
            _spawningWave = true;

            // Extra safety: if this is wave 1 and total count still below baseline, inject missing enemies before spawn
            if (CurrentWaveNumber == 1)
            {
                int baseline = BulletHeavenFortressDefense.Balance.BalanceConfig.FirstWaveEnemyCount;
                int have = _activeWave.TotalEnemyCount;
                if (have < baseline)
                {
                    // choose a source enemy data
                    EnemyData source = defaultEnemyData;
                    if (source == null)
                    {
                        // try any existing spawn entry
                        var sp = _activeWave.Spawns;
                        if (sp != null && sp.Count > 0)
                        {
                            source = sp[0].enemyData;
                        }
                    }
                    if (source != null)
                    {
                        int need = baseline - have;
                        Debug.Log($"[WaveManager] Injecting {need} missing enemies to reach baseline {baseline} for Wave 1 (had {have}).");
                        var extra = new Data.WaveSpawnEntry
                        {
                            enemyData = source,
                            count = need,
                            spawnInterval = 0.2f,
                            spawnPointId = -1,
                            spawnAlongRightEdge = true
                        };
                        if (_activeWave is BulletHeavenFortressDefense.Data.WaveData wd)
                        {
                            wd.AddSpawnEntry(extra);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[WaveManager] Cannot inject missing enemies for Wave 1 (no enemyData source).", this);
                    }
                }
            }

            if (_activeWave.TotalEnemyCount <= 0)
            {
                if (verboseLogging) Debug.Log($"[WaveManager] Wave {CurrentWaveNumber} has zero TotalEnemyCount – skipping spawn.");
                _spawningWave = false;
                yield break;
            }

            if (verboseLogging) Debug.Log($"[WaveManager] Wave {CurrentWaveNumber} spawning. Expected count: {_activeWave.TotalEnemyCount}");
            yield return StartCoroutine(SpawnWaveEntries(_activeWave));
            if (verboseLogging) Debug.Log($"[WaveManager] Wave {CurrentWaveNumber} spawn routine finished. Tracked alive now: {_activeWaveEnemyIds.Count}");
            _spawningWave = false;

            while (_activeWaveEnemyIds.Count > 0)
            {
                PhaseTimerUpdated?.Invoke(_currentPhase, -1f);
                yield return null;
            }
        }

        private IEnumerator RunPhaseTimer(float duration, bool manualAdvance)
        {
            _phaseTimer = duration;
            _advanceRequested = false;

            if (manualAdvance)
            {
                PhaseTimerUpdated?.Invoke(_currentPhase, -1f);
                while (!_advanceRequested)
                {
                    yield return null;
                }

                _advanceRequested = false;
                yield break;
            }

            if (duration <= 0f)
            {
                PhaseTimerUpdated?.Invoke(_currentPhase, 0f);
                yield break;
            }

            while (_phaseTimer > 0f && !_advanceRequested)
            {
                PhaseTimerUpdated?.Invoke(_currentPhase, _phaseTimer);
                yield return null;
                _phaseTimer -= Time.deltaTime;
            }

            _advanceRequested = false;
            _phaseTimer = Mathf.Max(0f, _phaseTimer);
            PhaseTimerUpdated?.Invoke(_currentPhase, _phaseTimer);
        }

        private IEnumerator SpawnWaveEntries(WaveData wave)
        {
            var spawns = wave.Spawns;
            if (spawns == null)
            {
                if (verboseLogging) Debug.Log("[WaveManager] SpawnWaveEntries: wave.Spawns is null – nothing to spawn.");
                yield break;
            }

            int spawned = 0;
            for (int i = 0; i < spawns.Count; i++)
            {
                var entry = spawns[i];
                if (entry.enemyData == null || entry.count <= 0)
                {
                    if (verboseLogging) Debug.Log($"[WaveManager] Skipping spawn entry index={i} enemyDataNull={entry.enemyData==null} count={entry.count}");
                    continue;
                }

                bool largeEntry = progressiveLargeWaveSpawning && entry.count >= Mathf.Max(1, progressiveSpawnThreshold);
                if (entry.spawnAlongRightEdge)
                {
                    // Burst spawn path: optionally progressive if very large.
                    int totalPoints = SpawnSystem.Instance.EnsureEdgeSpawnPoints(entry.count);
                    float spacing = Mathf.Max(SpawnSystem.Instance.RightEdgeHorizontalSpacing, 0.01f);
                    if (spacing <= 0.011f)
                    {
                        Debug.LogWarning("[WaveManager] RightEdgeHorizontalSpacing is very small; enemies may appear behind each other. Using minimal spacing.");
                    }
                    int perFrameBudget = Mathf.Max(1, maxEnemySpawnsPerFrame);
                    int spawnedThisFrame = 0;
                    for (int j = 0; j < entry.count; j++)
                    {
                        EnemyController enemy = null;
                        if (totalPoints > 0)
                        {
                            float t = (j + 0.5f) / entry.count;
                            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (totalPoints - 1)), 0, totalPoints - 1);
                            var point = SpawnSystem.Instance.GetEdgeSpawnPoint(idx);
                            if (point != null)
                            {
                                var pos = point.position;
                                float baseEdgeX = pos.x + rightEdgeExtraOffset;
                                if (forceRightEdgeColumn)
                                {
                                    float inset = (rightEdgeRandomInsetMax > 0f) ? UnityEngine.Random.value * rightEdgeRandomInsetMax : 0f;
                                    pos.x = baseEdgeX - inset;
                                }
                                else
                                {
                                    float dx = j * spacing; // legacy diagonal spread
                                    pos.x = baseEdgeX - dx;
                                }
                                // Small y jitter to avoid exact stacking
                                pos.y += (UnityEngine.Random.value - 0.5f) * 0.05f;
                                if (pos.x < minEnemySpawnX) pos.x = minEnemySpawnX;
                                if (verboseLogging) Debug.Log($"[WaveManager] Spawn burst (edge point) j={j}, idx={idx}, pos=({pos.x:F2},{pos.y:F2}) mode={(forceRightEdgeColumn ? "column" : "diagonal")}");
                                enemy = SpawnSystem.Instance.SpawnEnemyAtPosition(entry.enemyData, pos);
                            }
                            else
                            {
                                enemy = SpawnSystem.Instance.SpawnEnemyAtEdgeIndex(entry.enemyData, idx);
                            }
                        }
                        else
                        {
                            float t = (entry.count <= 1) ? UnityEngine.Random.value : (j + 0.5f) / entry.count;
                            var basePos = SpawnSystem.Instance.GetRightEdgePositionAtNormalizedY(t);
                            float baseEdgeX = basePos.x + rightEdgeExtraOffset;
                            if (forceRightEdgeColumn)
                            {
                                float inset = (rightEdgeRandomInsetMax > 0f) ? UnityEngine.Random.value * rightEdgeRandomInsetMax : 0f;
                                basePos.x = baseEdgeX - inset;
                            }
                            else
                            {
                                float dx = j * spacing; // legacy diagonal spread
                                basePos.x = baseEdgeX - dx;
                            }
                            if (basePos.x < minEnemySpawnX) basePos.x = minEnemySpawnX;
                            basePos.y += (UnityEngine.Random.value - 0.5f) * 0.05f; // slight y jitter
                            if (verboseLogging) Debug.Log($"[WaveManager] Spawn burst (normalizedY) j={j}, t={t:F2}, pos=({basePos.x:F2},{basePos.y:F2}) mode={(forceRightEdgeColumn ? "column" : "diagonal")}");
                            enemy = SpawnSystem.Instance.SpawnEnemyAtPosition(entry.enemyData, basePos);
                        }

                        if (enemy != null)
                        {
                            _activeWaveEnemyIds.Add(enemy.GetInstanceID());
                            spawned++;
                            enemy.ApplyBalanceOverrides(_rapidBaseDamage, CurrentWaveNumber);
                        }

                        // Progressive yield: only if large entry and either interval is zero OR we ignore interval requirement
                        if (largeEntry && (!throttleOnlyWhenNoInterval || entry.spawnInterval <= 0f))
                        {
                            spawnedThisFrame++;
                            if (spawnedThisFrame >= perFrameBudget)
                            {
                                if (verboseProgressiveLogs) Debug.Log($"[WaveManager][Progressive] Yield frame after {spawnedThisFrame} edge spawns (entry.count={entry.count}).");
                                spawnedThisFrame = 0;
                                yield return null; // let frame breathe
                            }
                        }
                    }

                    // Single wait after the burst
                    float wait = entry.spawnInterval;
                    if (wait > 0f)
                    {
                        yield return new WaitForSeconds(wait);
                    }
                    else
                    {
                        // Only a single null yield if we were NOT doing progressive yields inside loop
                        if (!(largeEntry && (!throttleOnlyWhenNoInterval || entry.spawnInterval <= 0f)))
                        {
                            yield return null;
                        }
                    }
                }
                else
                {
                    // Lane-based: preserve per-enemy interval behavior
                    int perFrameBudget = Mathf.Max(1, maxEnemySpawnsPerFrame);
                    int spawnedThisFrame = 0;
                    for (int j = 0; j < entry.count; j++)
                    {
                        EnemyController enemy = null;
                        // Fallback: if lanes are not configured (0 or 1 point) or spawnPointId is invalid, distribute along right edge instead
                        int laneCount = SpawnSystem.HasInstance ? SpawnSystem.Instance.EnemyLaneSpawnPointCount : 0;
                        bool invalidLane = entry.spawnPointId < 0 || laneCount <= 1;
                        if (invalidLane)
                        {
                            float t = (entry.count <= 1) ? UnityEngine.Random.value : (j + 0.5f) / entry.count;
                            var pos = SpawnSystem.Instance.GetRightEdgePositionAtNormalizedY(t);
                            // slight jitter to avoid stacking
                            pos.y += (UnityEngine.Random.value - 0.5f) * 0.05f;
                            pos.x += rightEdgeExtraOffset;
                            if (pos.x < minEnemySpawnX) pos.x = minEnemySpawnX;
                            if (verboseLogging) Debug.Log($"[WaveManager] Lane fallback → right-edge j={j}, t={t:F2}, pos=({pos.x:F2},{pos.y:F2})");
                            enemy = SpawnSystem.Instance.SpawnEnemyAtPosition(entry.enemyData, pos);
                        }
                        else
                        {
                            enemy = SpawnEnemy(entry.enemyData, entry.spawnPointId);
                        }
                        if (enemy != null)
                        {
                            _activeWaveEnemyIds.Add(enemy.GetInstanceID());
                            spawned++;
                        }

                        float wait = entry.spawnInterval;
                        if (wait > 0f)
                        {
                            // Normal timed spawning already spreads load; just wait.
                            yield return new WaitForSeconds(wait);
                        }
                        else
                        {
                            if (largeEntry && (!throttleOnlyWhenNoInterval || wait <= 0f))
                            {
                                spawnedThisFrame++;
                                if (spawnedThisFrame >= perFrameBudget)
                                {
                                    if (verboseProgressiveLogs) Debug.Log($"[WaveManager][Progressive] Yield frame after {spawnedThisFrame} lane spawns (entry.count={entry.count}).");
                                    spawnedThisFrame = 0;
                                    yield return null;
                                }
                            }
                            else
                            {
                                yield return null; // legacy behavior small batches
                            }
                        }
                    }
                }
            }

            if (verboseLogging) Debug.Log($"[WaveManager] Spawned {spawned} enemies for Wave {CurrentWaveNumber}.");
            if (spawned == 0)
            {
                Debug.LogWarning($"[WaveManager] SpawnWaveEntries finished but spawned == 0 (wave {CurrentWaveNumber}). Check defaultEnemyData & wave asset integrity.");
            }
        }

        private void DetermineRapidTowerBaseDamage()
        {
            _rapidBaseDamage = 0f;
            var tm = TowerManager.HasInstance ? TowerManager.Instance : null;
            if (tm == null) return;
            var list = tm.UnlockedTowers;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || string.IsNullOrEmpty(t.DisplayName)) continue;
                if (t.DisplayName.ToLower().Contains("rapid"))
                {
                    _rapidBaseDamage = t.Damage;
                    break;
                }
            }
        }

        private void GenerateAutoWaves()
        {
            if (defaultEnemyData == null)
            {
                Debug.LogWarning("WaveManager: Cannot auto-generate waves without defaultEnemyData.");
                return;
            }
            if (waveSequence == null)
            {
                waveSequence = ScriptableObject.CreateInstance<WaveSequence>();
            }
            for (int wave = waveSequence.Waves.Count + 1; wave <= targetAutoWaves; wave++)
            {
                GenerateSingleAutoWave(wave);
            }
        }

        private void GenerateSingleAutoWave(int waveNumber)
        {
            if (waveSequence == null) return;
            var wd = ScriptableObject.CreateInstance<WaveData>();
            int count = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyCountForWave(waveNumber);
            if (verboseLogging) Debug.Log($"[WaveManager] Auto-gen wave {waveNumber}: enemyCount={count}");
            float interval = Mathf.Clamp((count <= 40 ? 0.4f : 0.25f), 0.05f, 1f);
            var entry = new Data.WaveSpawnEntry
            {
                enemyData = defaultEnemyData,
                count = count,
                spawnInterval = interval,
                spawnPointId = -1,
                spawnAlongRightEdge = true
            };
            wd.ConfigurePhaseDurations(autoShopSeconds, autoPrepSeconds, autoPostSeconds);
            wd.ClearSpawns();
            wd.AddSpawnEntry(entry);
            waveSequence.Waves.Add(wd);
        }

        private EnemyController SpawnEnemy(EnemyData enemyData, int spawnPointId)
        {
            if (!SpawnSystem.HasInstance)
            {
                Debug.LogWarning("WaveManager: No SpawnSystem available.", this);
                return null;
            }

            var enemy = SpawnSystem.Instance.SpawnEnemy(enemyData, spawnPointId);
            if (enemy == null)
            {
                Debug.LogWarning("WaveManager: Failed to spawn enemy.", this);
            }

            return enemy;
        }

        private IEnumerator WaitForDelay(float delay)
        {
            _phaseTimer = delay;
            while (_phaseTimer > 0f)
            {
                PhaseTimerUpdated?.Invoke(_currentPhase, _phaseTimer);
                yield return null;
                _phaseTimer -= Time.deltaTime;
            }

            _phaseTimer = 0f;
            PhaseTimerUpdated?.Invoke(_currentPhase, _phaseTimer);
        }

        private void HandleEnemyDefeated(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            int id = enemy.GetInstanceID();
            if (_activeWaveEnemyIds.Contains(id))
            {
                _activeWaveEnemyIds.Remove(id);
            }

            // Increment global kill counter for this run
            _totalKills = Mathf.Max(0, _totalKills + 1);
        }

        private void SetPhase(WavePhase phase)
        {
            if (_currentPhase == phase)
            {
                return;
            }

            _currentPhase = phase;
            _phaseTimer = 0f;
            onPhaseChanged?.Raise();
            PhaseChanged?.Invoke(_currentPhase);

            if (GameManager.HasInstance)
            {
                GameManager.Instance.SyncWavePhase(_currentPhase);
            }
        }
    }
}
