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

        public GameState CurrentState { get; private set; }
        public event Action<GameState> StateChanged;

        protected override void Awake()
        {
            base.Awake();
            CurrentState = initialState;
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
            // Ensure time scale restored when starting a fresh run
            if (Time.timeScale != 1f) Time.timeScale = 1f;
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
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
                return;
            }

            SetState(GameState.MainMenu);
            if (Time.timeScale != 1f) Time.timeScale = 1f; // restore normal speed in menu

            // Reactivate main menu overlay if exists
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
        }
    }
}
