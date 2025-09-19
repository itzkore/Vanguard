using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class SlowProjectile : MonoBehaviour, ITowerProjectile
    {
        [SerializeField] private float speed = 6f;
        [SerializeField] private float maxLifetime = 5f;
    [SerializeField, Tooltip("Runtime slow factor (speed multiplier) injected from TowerData each shot.")] private float slowFactor = 0.5f;
    [SerializeField] private float slowDuration = 2f;
        [SerializeField] private float damageMultiplier = 0.5f;

        private float _baseDamage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.right;
        private string _poolId;

        public void Initialize(TowerData source, Vector3 direction, string poolId)
        {
            _baseDamage = (source?.Damage ?? 0f) * Mathf.Max(0f, damageMultiplier);
            _damageType = source != null ? source.DamageType : DamageType.Physical;
            _lifeTimer = maxLifetime;
            _poolId = poolId;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            transform.right = _direction;
            if (source != null && source.IsSlow)
            {
                // slowFactor & slowDuration were already injected from TowerBehaviour prior to Initialize when spawned.
            }
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
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                if (_baseDamage > 0.01f)
                {
                    Systems.DamageSystem.Instance.ApplyDamage(damageable, _baseDamage, _damageType);
                }

                if (other.TryGetComponent<EnemyController>(out var enemy))
                {
                    enemy.ApplySlow(Mathf.Clamp01(slowFactor), Mathf.Max(0f, slowDuration));
                    Debug.Log($"[Slow] applied factor={slowFactor:F2} dur={slowDuration:F2}s dmg={_baseDamage:F1}");
                }
            }

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
