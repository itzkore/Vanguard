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

        public bool IsAlive => _currentHealth > 0;

        private void Awake()
        {
            _currentHealth = maxHealth;
        }

        public void TakeDamage(float amount, Data.DamageType damageType)
        {
            if (!IsAlive)
            {
                return;
            }

            _currentHealth -= Mathf.RoundToInt(amount);
            onBaseDamaged?.Raise();

            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                onBaseDestroyed?.Raise();
                GameManager.Instance.EndRun();
            }
        }
    }
}
