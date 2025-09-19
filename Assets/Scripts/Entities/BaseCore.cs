using System;
using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Entities
{
    public class BaseCore : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private GameEvent onBaseDamaged;
        [SerializeField] private GameEvent onBaseDestroyed;

        private int _currentHealth;

        public static BaseCore Instance { get; private set; }
        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;
        public event Action<int, int> HealthChanged;

        private void Awake()
        {
            Instance = this;
            _currentHealth = maxHealth;
            NotifyHealthChanged();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TakeDamage(float amount, Data.DamageType damageType)
        {
            if (!IsAlive)
            {
                return;
            }

            _currentHealth -= Mathf.RoundToInt(amount);
            if (_currentHealth < 0) _currentHealth = 0;
            Debug.Log($"[BaseCore] TakeDamage {amount} -> {_currentHealth}/{maxHealth}");
            onBaseDamaged?.Raise();
            NotifyHealthChanged();

            if (_currentHealth <= 0)
            {
                if (IsAlive) return; // guard
                onBaseDestroyed?.Raise();
                NotifyHealthChanged();
                if (GameManager.HasInstance && GameManager.Instance.CurrentState != Managers.GameManager.GameState.GameOver)
                {
                    Debug.Log("[BaseCore] Core destroyed -> triggering GameOver");
                    GameManager.Instance.EndRun();
                }
            }
        }

        public void RestoreFullHealth()
        {
            _currentHealth = maxHealth;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(_currentHealth, maxHealth);
        }
    }
}
