using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        private static readonly List<EnemyController> _activeEnemies = new();

        [SerializeField] private Rigidbody2D body;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float contactDamage = 10f;

        private EnemyData _data;
        private float _currentHealth;

        public static IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;
        public bool IsAlive => _currentHealth > 0f;
        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            if (!_activeEnemies.Contains(this))
            {
                _activeEnemies.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeEnemies.Remove(this);
        }

        private void OnDestroy()
        {
            _activeEnemies.Remove(this);
        }

        public void Initialize(EnemyData data)
        {
            _data = data;
            _currentHealth = data?.Health ?? 0f;
            moveSpeed = data?.MoveSpeed ?? moveSpeed;
            contactDamage = Mathf.Max(contactDamage, 0f);
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsAlive)
            {
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(contactDamage, DamageType.Physical);
                Die();
            }
        }
    }
}
