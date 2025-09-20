using UnityEngine;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Managers; // ObjectPoolManager
using BulletHeavenFortressDefense.Data; // DamageType

namespace BulletHeavenFortressDefense.Projectiles
{
    /// <summary>
    /// Simple enemy-fired projectile that travels in a direction until lifetime expires or it hits an IDamageable on the player's side.
    /// (Restored minimal version; adjust damage / speed externally when spawning.)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 5f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float damage = 4f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [Header("Auto Scale")]
        [SerializeField, Tooltip("If true, projectile auto scales uniformly to targetWorldWidth.")] private bool autoScale = false;
        [SerializeField, Tooltip("Desired world width when autoScale enabled.")] private float targetWorldWidth = 0.04f;

        private Vector3 _direction = Vector3.left;
        private float _lifeTimer;
        private string _poolId;
        private bool _initialized;

        // Legacy signature used in some spawn code: (damage, type, direction, speed, poolId)
        public void Initialize(float damageOverride, DamageType type, Vector3 direction, float speedOverride, string poolId)
        {
            damageType = type;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.left;
            if (speedOverride > 0f) speed = speedOverride;
            if (damageOverride >= 0f) damage = damageOverride;
            _lifeTimer = lifetime;
            _poolId = poolId;
            _initialized = true;
            transform.right = _direction;
            ApplyAutoScaleIfNeeded();
            ClampVisualScaleIfNeeded();
        }

        // Backwards compatible minimal API (if some code still calls old signature)
        public void Initialize(Vector3 direction, float speedOverride, float damageOverride, string poolId)
        {
            Initialize(damageOverride, damageType, direction, speedOverride, poolId);
        }

        public void SetMaxLifetime(float value) { lifetime = Mathf.Max(0.01f, value); _lifeTimer = Mathf.Min(_lifeTimer, lifetime); }
        public void SetAutoScale(bool enabled) { autoScale = enabled; ApplyAutoScaleIfNeeded(); }
        public void SetTargetWorldWidth(float width) { targetWorldWidth = Mathf.Max(0.001f, width); ApplyAutoScaleIfNeeded(); }

        private void Update()
        {
            if (!_initialized) return;
            Vector3 start = transform.position;
            Vector3 move = _direction * speed * Time.deltaTime;
            transform.position = start + move;
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Despawn();
            }

            // Overlap fallback: if we are inside a wall/base collider but OnTriggerEnter didn't fire (spawned inside or tunneling)
            var hits = Physics2D.OverlapCircleAll(transform.position, 0.06f);
            if (hits != null)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    var c = hits[i];
                    if (c == null) continue;
                    if (c.TryGetComponent<IDamageable>(out var dmg) && IsValidDamageTarget(dmg))
                    {
                        Systems.DamageSystem.Instance.ApplyDamage(dmg, damage, damageType);
                        Despawn();
                        return;
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
            if (IsValidDamageTarget(damageable))
            {
                Systems.DamageSystem.Instance.ApplyDamage(damageable, damage, damageType);
                Despawn();
            }
        }

        private void Despawn()
        {
            if (!string.IsNullOrEmpty(_poolId) && ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Release(_poolId, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ApplyAutoScaleIfNeeded()
        {
            if (!autoScale) return;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.bounds.size.x <= 0.0001f) return;
            float currentWidth = sr.bounds.size.x;
            float scaleMult = targetWorldWidth / currentWidth;
            transform.localScale = new Vector3(scaleMult, scaleMult, 1f);
        }

        // If a projectile prefab (or dynamically created fallback sprite) ends up huge (e.g. > 0.25 world units width),
        // auto-enable scaling and shrink it to a sane width (targetWorldWidth or default 0.04f).
        private void ClampVisualScaleIfNeeded()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            float w = sr.bounds.size.x;
            if (w > 0.25f)
            {
                if (!autoScale) // force enable autoscale if not already
                {
                    autoScale = true;
                    if (targetWorldWidth <= 0.0001f) targetWorldWidth = 0.04f;
                }
                ApplyAutoScaleIfNeeded();
            }
            else if (autoScale && w > 0.0001f)
            {
                // Even if not extreme, enforce target width if slight prefab drift occurred
                ApplyAutoScaleIfNeeded();
            }
        }

        // Enemy projectiles should only damage fortress defenses or the base core (player side), never other enemies.
        private bool IsValidDamageTarget(IDamageable dmg)
        {
            return dmg is Fortress.FortressWall || dmg is BaseCore; // BaseCore in same namespace
        }
    }
}
