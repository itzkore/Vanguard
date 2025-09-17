using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class GameManager : Singleton<GameManager>
    {
        public enum GameState
        {
            Boot,
            MainMenu,
            PreparingWave,
            InWave,
            GameOver
        }

        [SerializeField] private GameState initialState = GameState.Boot;
        [SerializeField] private Utilities.GameEvent onRunStarted;
        [SerializeField] private Utilities.GameEvent onRunEnded;

        public GameState CurrentState { get; private set; }

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
        }

        public void StartRun()
        {
            SetState(GameState.PreparingWave);
            onRunStarted?.Raise();
        }

        public void BeginWave()
        {
            SetState(GameState.InWave);
        }

        public void EndRun()
        {
            SetState(GameState.GameOver);
            onRunEnded?.Raise();
        }

        public void ReturnToMenu()
        {
            SetState(GameState.MainMenu);
        }

        private void SetState(GameState targetState)
        {
            if (CurrentState == targetState)
            {
                return;
            }

            CurrentState = targetState;
            Debug.Log($"Game state changed to {CurrentState}");
        }
    }
}
