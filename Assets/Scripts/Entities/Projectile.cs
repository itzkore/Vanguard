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

        public void Initialize(TowerData source)
        {
            _damage = source?.Damage ?? 0f;
            _damageType = source != null ? source.DamageType : DamageType.Physical;
            _lifeTimer = maxLifetime;
        }

        private void Update()
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
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
