using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private Text baseHealthText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text waveText;

        public void RefreshBaseHealth(int current, int max)
        {
            if (baseHealthText != null)
            {
                baseHealthText.text = $"Base: {current}/{max}";
            }
        }

        public void RefreshEnergy()
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {EconomySystem.Instance.CurrentEnergy}";
            }
        }

        public void RefreshWave(int index)
        {
            if (waveText != null)
            {
                waveText.text = $"Wave {index + 1}";
            }
        }
    }
}
