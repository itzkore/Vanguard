using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, ITowerProjectile
    {
        [SerializeField] private float speed = 8f;
        [SerializeField] private float maxLifetime = 5f;

        private float _damage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.right;
        private string _poolId;

    // Expose read-only accessors so other systems can match this projectile's behavior
    public float Speed => speed;
    public float MaxLifetime => maxLifetime;

        public void Initialize(TowerData source, Vector3 direction, string poolId)
        {
            _damage = source?.Damage ?? 0f;
            _damageType = source != null ? source.DamageType : DamageType.Physical;
            _lifeTimer = maxLifetime;
            _poolId = poolId;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            transform.right = _direction;
        }

        private void Update()
        {
            transform.position += _direction * speed * Time.deltaTime;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable))
            {
                return;
            }

            Systems.DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
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
