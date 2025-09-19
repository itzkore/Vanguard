using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class RunControlPanel : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Text buttonLabel;
        [SerializeField] private string startLabel = "Start Run";
        [SerializeField] private string readyLabel = "Ready";
        [SerializeField] private string combatLabel = "In Combat";

        private bool _wired;

        private void Awake()
        {
            TryWire();
        }

        private void OnEnable()
        {
            TryWire();
            Subscribe();
            RefreshButtonState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(Button start)
        {
            startButton = start;
            CacheLabel();
            TryWire();
            RefreshButtonState();
        }

        private void TryWire()
        {
            if (_wired || startButton == null)
            {
                return;
            }

            CacheLabel();
            startButton.onClick.AddListener(OnStartClicked);
            _wired = true;
        }

        private void CacheLabel()
        {
            if (buttonLabel == null && startButton != null)
            {
                buttonLabel = startButton.GetComponentInChildren<Text>();
            }
        }

        private void Subscribe()
        {
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.PhaseChanged += OnPhaseChanged;
            }
        }

        private void Unsubscribe()
        {
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
        }

        private void OnPhaseChanged(WaveManager.WavePhase phase)
        {
            RefreshButtonState();
        }

        private void RefreshButtonState()
        {
            if (startButton == null)
            {
                return;
            }

            var state = GameManager.HasInstance ? GameManager.Instance.CurrentState : GameManager.GameState.MainMenu;
            string label = startLabel;
            bool interactable = true;

            switch (state)
            {
                case GameManager.GameState.MainMenu:
                    label = startLabel;
                    interactable = true;
                    break;
                case GameManager.GameState.ShopPhase:
                case GameManager.GameState.PreparationPhase:
                    label = readyLabel;
                    interactable = WaveManager.HasInstance && WaveManager.Instance.CanAdvancePhase;
                    break;
                case GameManager.GameState.CombatPhase:
                    label = combatLabel;
                    interactable = false;
                    break;
                case GameManager.GameState.Completed:
                    label = readyLabel;
                    interactable = WaveManager.HasInstance && WaveManager.Instance.CanAdvancePhase;
                    break;
                case GameManager.GameState.GameOver:
                    label = startLabel;
                    interactable = true;
                    break;
            }

            if (buttonLabel != null)
            {
                buttonLabel.text = label;
            }

            startButton.interactable = interactable;
        }

        private void OnStartClicked()
        {
            var gameState = GameManager.HasInstance ? GameManager.Instance.CurrentState : GameManager.GameState.MainMenu;

            if (gameState == GameManager.GameState.MainMenu || gameState == GameManager.GameState.GameOver)
            {
                GameManager.Instance?.StartRun();
                return;
            }

            if (WaveManager.HasInstance && WaveManager.Instance.CanAdvancePhase)
            {
                WaveManager.Instance.RequestAdvancePhase();
            }
        }
    }
}
