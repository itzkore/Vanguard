using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float maxLifetime = 5f;
    [SerializeField, Tooltip("Uniform visual scale factor applied once per instance for readability (used if no renderer found). ")] private float visualScaleFactor = 0.12f;
    [SerializeField, Tooltip("Target world-space width for the projectile visual in units. If > 0, we scale the instance so its SpriteRenderer bounds width equals this.")] private float targetWorldWidth = 0.024f;

        private float _damage;
        private DamageType _damageType;
        private float _lifeTimer;
        private Vector3 _direction = Vector3.left;
        private float _speed = 8f;
        private string _poolId;
    private Vector3 _originalScale;
    private bool _scaleApplied;
    private bool _autoScale = true;

        private void Awake()
        {
            _originalScale = transform.localScale;
            var col = GetComponent<Collider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            // Ensure a Rigidbody2D exists so trigger collisions fire with static colliders
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;
        }

        public void Initialize(float damage, DamageType damageType, Vector3 direction, float speed, string poolId)
        {
            _damage = damage;
            _damageType = damageType;
            _lifeTimer = maxLifetime;
            _poolId = poolId;
            _speed = Mathf.Max(6f, speed); // prevent extremely slow bullets
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.left;
            transform.right = _direction;
            // Ensure we have a trigger collider for hits
            var col = GetComponent<Collider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            // Match turret projectile style: transform-based movement, collider-only

            // Apply consistent visual size exactly once per pooled instance (unless disabled)
            if (_autoScale && !_scaleApplied)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null && targetWorldWidth > 0f)
                {
                    var width = sr.bounds.size.x;
                    if (width > 0.0001f)
                    {
                        float factor = targetWorldWidth / width;
                        transform.localScale = _originalScale * factor;
                        _scaleApplied = true;
                    }
                }
                if (!_scaleApplied)
                {
                    // Fallback: simple percentage scale
                    transform.localScale = _originalScale * Mathf.Clamp(visualScaleFactor, 0.01f, 1f);
                    _scaleApplied = true;
                }

                // Match collider radius to target visual width (world space)
                var cc = GetComponent<CircleCollider2D>();
                if (cc != null)
                {
                    float worldScale = Mathf.Max(0.0001f, Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
                    float desiredWorldRadius = (targetWorldWidth > 0f ? targetWorldWidth * 0.5f : 0.02f);
                    cc.radius = desiredWorldRadius / worldScale;
                }
            }
        }

        public void SetAutoScale(bool enabled)
        {
            _autoScale = enabled;
            if (!_autoScale)
            {
                // Reset so we do not apply scaling on next Initialize
                _scaleApplied = true;
            }
            else
            {
                // Allow scaling to happen on next Initialize
                _scaleApplied = false;
            }
        }

        public void SetTargetWorldWidth(float width)
        {
            targetWorldWidth = Mathf.Max(0.001f, width);
            // Recompute on next Initialize
            if (_autoScale)
            {
                _scaleApplied = false;
            }
        }

        public void SetMaxLifetime(float lifetime)
        {
            maxLifetime = Mathf.Max(0.05f, lifetime);
        }

        private void Update()
        {
            // Simple transform movement to match rapid turret behavior
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

            if (DamageSystem.HasInstance)
            {
                DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
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
