using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    [DisallowMultipleComponent]
    public class EuroDisplay : MonoBehaviour
    {
    [SerializeField] private Text targetText;
    private Coroutine _waitRoutine;
    private bool _subscribed;

        private void Awake()
        {
            if (targetText == null) targetText = GetComponent<Text>();
            if (targetText == null) targetText = GetComponentInChildren<Text>(true);
        }

        private void OnEnable()
        {
            TrySubscribeOrWait();
        }

        private void OnDisable()
        {
            if (_subscribed && EconomySystem.HasInstance)
            {
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
            }
            _subscribed = false;
            if (_waitRoutine != null)
            {
                StopCoroutine(_waitRoutine);
                _waitRoutine = null;
            }
        }

        private void TrySubscribeOrWait()
        {
            if (EconomySystem.HasInstance)
            {
                if (!_subscribed)
                {
                    EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
                    _subscribed = true;
                }
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }
            else if (_waitRoutine == null)
            {
                _waitRoutine = StartCoroutine(WaitForEconomy());
            }
        }

        private System.Collections.IEnumerator WaitForEconomy()
        {
            // Poll a few frames until EconomySystem exists
            while (!EconomySystem.HasInstance)
            {
                yield return null;
            }
            _waitRoutine = null;
            TrySubscribeOrWait();
        }

        private void OnEnergyChanged(int value)
        {
            if (targetText != null)
            {
                targetText.text = $"€ {value}";
            }
        }
    }
}
