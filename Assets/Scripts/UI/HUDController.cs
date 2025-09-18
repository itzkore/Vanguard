using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class HUDController : MonoBehaviour
    {
    [SerializeField] private Text baseHealthText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text waveText;

        public Text BaseHealthText => baseHealthText;
        public Text EnergyText => energyText;
        public Text WaveText => waveText;

        private void OnEnable()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                BaseCore.Instance.HealthChanged += OnBaseHealthChanged;
                OnBaseHealthChanged(BaseCore.Instance.CurrentHealth, BaseCore.Instance.MaxHealth);
            }

            if (EconomySystem.Instance != null && energyText != null)
            {
                EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.WaveStarted += OnWaveStarted;
                if (WaveManager.Instance.CurrentWaveNumber > 0)
                {
                    OnWaveStarted(WaveManager.Instance.CurrentWaveNumber);
                }
                else if (waveText != null)
                {
                    waveText.text = "Wave 0";
                }
            }
        }

        private void OnDisable()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                BaseCore.Instance.HealthChanged -= OnBaseHealthChanged;
            }

            if (EconomySystem.Instance != null && energyText != null)
            {
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
            }

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.WaveStarted -= OnWaveStarted;
            }
        }

        public void Configure(Text baseLabel, Text energyLabel, Text waveLabel)
        {
            baseHealthText = baseLabel;
            energyText = energyLabel;
            waveText = waveLabel;
            ApplyInitialValues();
        }

        private void ApplyInitialValues()
        {
            if (BaseCore.Instance != null && baseHealthText != null)
            {
                OnBaseHealthChanged(BaseCore.Instance.CurrentHealth, BaseCore.Instance.MaxHealth);
            }

            if (EconomySystem.Instance != null && energyText != null)
            {
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }

            if (WaveManager.Instance != null && WaveManager.Instance.CurrentWaveNumber > 0)
            {
                OnWaveStarted(WaveManager.Instance.CurrentWaveNumber);
            }
            else if (waveText != null)
            {
                waveText.text = "Wave 0";
            }
        }

        private void OnBaseHealthChanged(int current, int max)
        {
            if (baseHealthText != null)
            {
                baseHealthText.text = $"Base: {current}/{max}";
            }
        }

        private void OnEnergyChanged(int value)
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {value}";
            }
        }

        private void OnWaveStarted(int waveNumber)
        {
            if (waveText != null)
            {
                waveText.text = $"Wave {waveNumber}";
            }
        }
    }
}
