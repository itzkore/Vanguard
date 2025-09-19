using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class SplashProjectile : MonoBehaviour, ITowerProjectile
    {
        [SerializeField] private float speed = 7f;
        [SerializeField] private float maxLifetime = 4f;
    [SerializeField, Tooltip("Runtime current radius (set from TowerData each shot). ")] private float radius = 1.5f;
    [SerializeField] private float falloffExponent = 1f;
        [SerializeField] private LayerMask hitMask = ~0;

        private float _damage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.right;
        private string _poolId;
        private bool _exploded;

        private static readonly Collider2D[] _overlapResults = new Collider2D[64];

        public void Initialize(TowerData source, Vector3 direction, string poolId)
        {
            _damage = source?.Damage ?? 0f;
            _damageType = source != null ? source.DamageType : DamageType.Physical;
            _lifeTimer = maxLifetime;
            _poolId = poolId;
            _exploded = false;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            transform.right = _direction;
            // Inject scalable splash values
            if (source != null && source.IsSplash)
            {
                radius = ComputeScaledRadius(source, 1); // level passed implicitly via damage (already scaled at tower?) – We'll treat source.Damage unaffected.
                falloffExponent = source.SplashFalloffExponent;
            }
        }

        private float ComputeScaledRadius(TowerData data, int dummy)
        {
            // We don't know the tower level here; assume damage already scaled externally. For now Splash radius encoded by Damage field? Instead we will stash radius in damage via wrapper soon if needed.
            // Simpler: radius already set in TowerBehaviour before spawning projectile (we'll override there). This method kept for potential future use.
            return Mathf.Max(0f, radius);
        }

        private void Update()
        {
            transform.position += _direction * speed * Time.deltaTime;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Explode();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Explode();
        }

        private void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _overlapResults, hitMask);
            int damaged = 0;
            for (int i = 0; i < hitCount; i++)
            {
                var collider = _overlapResults[i];
                if (collider == null || !collider.TryGetComponent<IDamageable>(out var damageable))
                {
                    continue;
                }

                float dist = Vector2.Distance(transform.position, collider.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / Mathf.Max(0.0001f, radius));
                float multiplier = Mathf.Pow(falloff, Mathf.Max(0.01f, falloffExponent));
                float damage = _damage * multiplier;
                if (damage > 0.01f)
                {
                    Systems.DamageSystem.Instance.ApplyDamage(damageable, damage, _damageType);
                    damaged++;
                }
            }
            if (damaged > 0)
            {
                Debug.Log($"[Splash] radius={radius:F2} damaged={damaged}");
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
