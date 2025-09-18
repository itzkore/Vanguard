using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float maxLifetime = 5f;

        private float _damage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.left;
        private float _speed = 8f;
        private string _poolId;

        public void Initialize(float damage, DamageType damageType, Vector3 direction, float speed, string poolId)
        {
            _damage = damage;
            _damageType = damageType;
            _lifeTimer = maxLifetime;
            _poolId = poolId;
            _speed = speed;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.left;
            transform.right = _direction;
        }

        private void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Do not damage other enemies
            if (other.GetComponent<EnemyController>() != null)
            {
                return;
            }

            // Prefer damaging a specific wall segment if we hit a wall
            if (other.TryGetComponent<BulletHeavenFortressDefense.Fortress.FortressWall>(out var wall))
            {
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                wall.TakeDamageAtPoint(_damage, _damageType, hitPoint);
                Despawn();
                return;
            }

            if (!other.TryGetComponent<IDamageable>(out var damageable))
            {
                return;
            }

            DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
            Despawn();
        }

        private void Despawn()
        {
            if (!string.IsNullOrEmpty(_poolId) && ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Release(_poolId, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
