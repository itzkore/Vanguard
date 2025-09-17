using UnityEngine;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private float moveSpeed = 1.5f;

        private EnemyData _data;
        private float _currentHealth;

        public bool IsAlive => _currentHealth > 0f;

        public void Initialize(EnemyData data)
        {
            _data = data;
            _currentHealth = data?.Health ?? 0f;
            moveSpeed = data?.MoveSpeed ?? moveSpeed;
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            body?.MovePosition(transform.position + Vector3.left * moveSpeed * Time.deltaTime);
        }

        public void TakeDamage(float amount, DamageType damageType)
        {
            if (!IsAlive)
            {
                return;
            }

            float modifier = _data != null ? _data.GetResistanceModifier(damageType) : 1f;
            _currentHealth -= amount * modifier;

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            Systems.EconomySystem.Instance.Add(_data != null ? _data.Reward : 0);
            gameObject.SetActive(false);
        }
    }
}
