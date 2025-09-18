using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class RunControlPanel : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private bool _wired;

        private void Awake()
        {
            TryWire();
        }

        private void OnEnable()
        {
            TryWire();
        }

        public void Configure(Button start)
        {
            startButton = start;
            TryWire();
        }

        private void TryWire()
        {
            if (_wired || startButton == null)
            {
                return;
            }

            startButton.onClick.AddListener(OnStartClicked);
            _wired = true;
        }

        private void OnStartClicked()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StartRun();
            }

            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.StartSequence();
            }
        }
    }
}
