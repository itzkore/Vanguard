using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;

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
        private string _poolId;
        private bool _released;
        private float _baseMoveSpeed;
        private float _speedMultiplier = 1f;
        private float _slowTimer;

        public static IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;
        public bool IsAlive => _currentHealth > 0f;
        public Vector3 Position => transform.position;
        public float RemainingHealth => _currentHealth;
        public float DistanceToBaseSquared => BaseCore.Instance != null
            ? (transform.position - BaseCore.Instance.transform.position).sqrMagnitude
            : float.MaxValue;

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

        public void Initialize(EnemyData data, string poolId)
        {
            _data = data;
            _poolId = poolId;
            _released = false;

            _currentHealth = data?.Health ?? 0f;
            _baseMoveSpeed = data?.MoveSpeed ?? moveSpeed;
            moveSpeed = _baseMoveSpeed;
            contactDamage = data != null ? Mathf.Max(0f, data.ContactDamage) : contactDamage;
            _speedMultiplier = 1f;
            _slowTimer = 0f;

            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f)
                {
                    _speedMultiplier = 1f;
                }
            }

            float speed = _baseMoveSpeed * Mathf.Max(0.05f, _speedMultiplier);
            var newPos = (Vector3)(Vector2.left * speed * Time.deltaTime) + transform.position;
            if (body != null)
            {
                body.MovePosition(newPos);
            }
            else
            {
                transform.position = newPos;
            }
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

        public void ApplySlow(float slowFactor, float duration)
        {
            slowFactor = Mathf.Clamp(slowFactor, 0.05f, 1f);
            _speedMultiplier = Mathf.Min(_speedMultiplier, slowFactor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        private void Die()
        {
            Systems.EconomySystem.Instance.Add(_data != null ? _data.Reward : 0);
            Despawn();
        }

        private void Despawn()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _currentHealth = 0f;

            if (!string.IsNullOrEmpty(_poolId) && ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Release(_poolId, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
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
                Despawn();
            }
        }
    }
}
