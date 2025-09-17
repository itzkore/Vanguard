using UnityEngine;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 8f;
        [SerializeField] private float maxLifetime = 5f;

        private float _damage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.right;

        public void Initialize(TowerData source, Vector3 direction)
        {
            _damage = source?.Damage ?? 0f;
            _damageType = source != null ? source.DamageType : DamageType.Physical;
            _lifeTimer = maxLifetime;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            transform.right = _direction;
        }

        private void Update()
        {
            transform.position += _direction * speed * Time.deltaTime;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable))
            {
                return;
            }

            Systems.DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
            gameObject.SetActive(false);
        }
    }
}
