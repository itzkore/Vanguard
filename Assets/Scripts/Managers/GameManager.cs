using System;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.Managers
{
    public class GameManager : Singleton<GameManager>
    {
        // If true when a scene loads, we immediately start a new run (used by PauseMenu restart without returning to main menu).
        internal static bool PendingAutoRestartRun = false;
        public enum GameState
        {
            Boot,
            MainMenu,
            ShopPhase,
            PreparationPhase,
            CombatPhase,
            Completed,
            GameOver
        }

        [SerializeField] private GameState initialState = GameState.Boot;
        [SerializeField] private GameEvent onRunStarted;
    [SerializeField] private GameEvent onRunEnded;
    [Header("Main Menu Config")] 
    [SerializeField, Tooltip("If true, ReturnToMenu will load a scene instead of just switching state.")] private bool loadMainMenuScene = false;
    [SerializeField, Tooltip("Scene name to load when loadMainMenuScene is true.")] private string mainMenuSceneName = "MainMenu";
    [SerializeField, Header("Full Reset"), Tooltip("If true and loadMainMenuScene is FALSE, ReturnToMenu will still reload the active scene to ensure a clean reset.")] private bool forceSceneReloadOnReturnToMenu = true;
    [SerializeField, Tooltip("Optional explicit boot scene name to reload when forceSceneReloadOnReturnToMenu = true. Leave empty to use active scene.")] private string bootSceneName = "";

        public GameState CurrentState { get; private set; }
        public event Action<GameState> StateChanged;

    [Header("Game Speed")]
    [SerializeField, Range(0.1f, 3f), Tooltip("Base gameplay speed factor applied when not paused. Keep at 1 for normal; adjust only if you truly want to scale EVERYTHING (UI animations, projectiles, towers, etc.). Enemy-only slowdown now lives in EnemyPace.SpeedMultiplier.")]
    private float baseGameSpeed = 1f; // revert to normal speed; enemy pacing handled separately
        public float BaseGameSpeed => baseGameSpeed;

        /// <summary>
        /// Applies the configured base game speed to Time.timeScale if not paused.
        /// </summary>
        public void ApplyBaseGameSpeed()
        {
            // If currently paused (timescale 0) don't override
            if (Mathf.Approximately(Time.timeScale, 0f)) return;
            if (!Mathf.Approximately(Time.timeScale, baseGameSpeed))
            {
                Time.timeScale = baseGameSpeed;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            CurrentState = initialState;
            // Ensure initial speed (Boot state) uses baseGameSpeed
            ApplyBaseGameSpeed();
        }

        private void Start()
        {
            if (CurrentState == GameState.Boot)
            {
                SetState(GameState.MainMenu);
            }

            if (PendingAutoRestartRun)
            {
                Debug.Log("[GameManager] Detected PendingAutoRestartRun flag – starting new run automatically.");
                PendingAutoRestartRun = false;
                StartRun();
            }
        }

        public void StartRun()
        {
            if (_startingRun) { Debug.LogWarning("[GameManager] StartRun ignored (already starting)." ); return; }
            _startingRun = true;
            // Force-restore time scale even if it was set to 0f by GameOver (ApplyBaseGameSpeed previously early-exited on 0)
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = baseGameSpeed;
            }
            else
            {
                ApplyBaseGameSpeed();
            }
            // If we're starting a new run after a previous one ended, ensure the entire playfield is reset.
            // Conditions: coming from GameOver or Completed (player finished or died) OR we have ever reached a combat state before.
            if (_hasRunBefore)
            {
                FullResetPlayfield();
            }
            // Invoke registered resets instead of scanning scene (faster, deterministic)
            BulletHeavenFortressDefense.Utilities.RunResetRegistry.ResetAll();

            BaseCore.Instance?.RestoreFullHealth();
            EconomySystem.Instance?.ResetEnergy();

            // Hide main menu overlay if present
            var mm = UnityEngine.Object.FindObjectOfType<BulletHeavenFortressDefense.UI.MainMenuController>(includeInactive:true);
            if (mm != null) mm.gameObject.SetActive(false);

            SetState(GameState.ShopPhase);
            onRunStarted?.Raise();

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StartSequence();
            }

            _hasRunBefore = true;
            _startingRun = false;
        }

        public void EndRun()
        {
            SetState(GameState.GameOver);
            onRunEnded?.Raise();

            if (WaveManager.HasInstance)
            {
                // Capture statistics BEFORE resetting the sequence so GameOver UI can display them
                WaveManager.Instance.CaptureRunEnd();
                WaveManager.Instance.StopSequence();
            }
            // Freeze the game world while showing Game Over UI. NOTE: StartRun now force-restores from 0.
            Time.timeScale = 0f;
        }

        #region Full Reset Implementation
        private bool _hasRunBefore = false;
    private bool _startingRun = false; // guards against rapid double StartRun calls

        /// <summary>
        /// Performs a comprehensive cleanup of runtime-spawned gameplay objects so a fresh run starts from a clean slate.
        /// Destroys remaining enemies, projectiles, towers, and rebuilds the fortress layout.
        /// </summary>
        private void FullResetPlayfield()
        {
            try
            {
                // 1. Stop any wave spawning logic first to prevent repopulation while clearing.
                if (WaveManager.HasInstance)
                {
                    WaveManager.Instance.StopSequence();
                }

                // 2. Clear enemies (copy static list to avoid modification while iterating).
                var enemyList = BulletHeavenFortressDefense.Entities.EnemyController.ActiveEnemies;
                if (enemyList != null && enemyList.Count > 0)
                {
                    // Copy to avoid enumerator issues
                    var toClear = new System.Collections.Generic.List<BulletHeavenFortressDefense.Entities.EnemyController>(enemyList);
                    for (int i = 0; i < toClear.Count; i++)
                    {
                        var e = toClear[i];
                        if (e == null) continue;
                        var go = e.gameObject;
                        if (go != null) UnityEngine.Object.Destroy(go);
                    }
                }

                // 3. Clear projectiles (all ITowerProjectile implementors)
                var monos = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
                for (int i = 0; i < monos.Length; i++)
                {
                    var mb = monos[i];
                    if (mb == null) continue;
                    if (mb is BulletHeavenFortressDefense.Entities.ITowerProjectile)
                    {
                        UnityEngine.Object.Destroy(mb.gameObject);
                    }
                }

                // 4. Remove placed towers (destroy all TowerBehaviour instances)
                var towers = UnityEngine.Object.FindObjectsOfType<BulletHeavenFortressDefense.Entities.TowerBehaviour>(true);
                for (int i = 0; i < towers.Length; i++)
                {
                    var t = towers[i]; if (t == null) continue;
                    UnityEngine.Object.Destroy(t.gameObject);
                }

                // 5. Rebuild fortress (walls, mounts, core) so mounts are empty.
                if (Fortress.FortressManager.HasInstance)
                {
                    Fortress.FortressManager.Instance.RebuildFortress();
                }

                // 6. Clear targeting focus bookkeeping if coordinator exists (prevents stale enemy refs).
                var focus = UnityEngine.Object.FindObjectOfType<BulletHeavenFortressDefense.AI.TargetFocusCoordinator>();
                if (focus != null)
                {
                    // Easiest: destroy & let scene recreate / or keep (if persistent) and rely on internal cleanup.
                    // We prefer a soft clean: reflection search for dictionary field and clear it.
                    var fld = typeof(BulletHeavenFortressDefense.AI.TargetFocusCoordinator).GetField("_focusCounts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fld != null)
                    {
                        if (fld.GetValue(focus) is System.Collections.IDictionary dict)
                        {
                            dict.Clear();
                        }
                    }
                }

                // 7. Reset economy & core already handled in StartRun after this call; ensure base core health full if instance present.
                BaseCore.Instance?.RestoreFullHealth();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[GameManager] Exception during FullResetPlayfield: " + ex);
            }
        }
        #endregion

        public void ReturnToMenu()
        {
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StopSequence();
            }

            if (loadMainMenuScene && !string.IsNullOrEmpty(mainMenuSceneName))
            {
                if (Time.timeScale != 1f) Time.timeScale = 1f;
                // Replace immediate restore with base speed AFTER scene load (scene load sets timescale implicitly)
                PendingAutoRestartRun = false; // never auto-run when going to explicit main menu scene
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
                return;
            }
            // If we want a hard reset even without a dedicated main menu scene, reload current (or boot) scene.
            if (forceSceneReloadOnReturnToMenu)
            {
                if (Time.timeScale != 1f) Time.timeScale = 1f;
                // Scene reload -> after load Awake() will ApplyBaseGameSpeed
                PendingAutoRestartRun = false; // ensure no auto-start
                string sceneToLoad = bootSceneName;
                if (string.IsNullOrEmpty(sceneToLoad))
                {
                    sceneToLoad = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; // reload same scene to clear runtime state
                }
                Debug.Log("[GameManager] ReturnToMenu -> force scene reload: '" + sceneToLoad + "'");
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
                return;
            }

            // Fallback: soft return (legacy behavior) – switch state & show overlay
            SetState(GameState.MainMenu);
            ApplyBaseGameSpeed(); // ensure base speed active in menu
            PendingAutoRestartRun = false;

            var mm = UnityEngine.Object.FindObjectOfType<BulletHeavenFortressDefense.UI.MainMenuController>(includeInactive:true);
            if (mm != null) mm.gameObject.SetActive(true);
        }

        internal void SyncWavePhase(WaveManager.WavePhase phase)
        {
            switch (phase)
            {
                case WaveManager.WavePhase.Idle:
                    if (CurrentState != GameState.MainMenu)
                    {
                        SetState(GameState.MainMenu);
                    }
                    break;
                case WaveManager.WavePhase.Shop:
                    SetState(GameState.ShopPhase);
                    break;
                case WaveManager.WavePhase.Preparation:
                    SetState(GameState.PreparationPhase);
                    break;
                case WaveManager.WavePhase.Combat:
                    SetState(GameState.CombatPhase);
                    break;
                case WaveManager.WavePhase.Completed:
                    if (CurrentState != GameState.GameOver)
                    {
                        SetState(GameState.Completed);
                    }
                    break;
            }
        }

        private void SetState(GameState targetState)
        {
            if (CurrentState == targetState)
            {
                return;
            }

            CurrentState = targetState;
            Debug.Log($"Game state changed to {CurrentState}");
            StateChanged?.Invoke(CurrentState);
            // Re-apply base speed on every state change unless paused by external system
            if (!Mathf.Approximately(Time.timeScale, 0f))
            {
                ApplyBaseGameSpeed();
            }
        }
    }
}
