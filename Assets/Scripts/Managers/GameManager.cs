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
            // Ensure time scale restored to base (may have been paused or different state)
            ApplyBaseGameSpeed();
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
        }

        public void EndRun()
        {
            SetState(GameState.GameOver);
            onRunEnded?.Raise();

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StopSequence();
            }
            // Freeze the game world while showing Game Over UI
            Time.timeScale = 0f;
        }

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
