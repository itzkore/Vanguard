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
        [Header("Collision")]
        [SerializeField, Tooltip("Enable continuous sweep (CircleCast) to prevent tunneling at high speed.")] private bool useContinuousSweep = true;
        [SerializeField, Tooltip("Radius used for sweep; should roughly match collider extents.")] private float sweepRadius = 0.08f;
    // Viewport culling removed (was causing premature despawns missing targets)

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

            // Apply per-tower base projectile speed if provided (>0). This lets designers tune relative speeds
            // without having to duplicate projectile prefabs. If absent, retain prefab's serialized speed.
            if (source != null && source.ProjectileSpeedBase > 0f)
            {
                speed = source.ProjectileSpeedBase;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 startPos = transform.position;
            Vector3 move = _direction * speed * dt;
            Vector3 endPos = startPos + move;

            if (useContinuousSweep)
            {
                float dist = move.magnitude;
                if (dist > 0f)
                {
                    var hits = Physics2D.CircleCastAll(startPos, sweepRadius, _direction, dist);
                    if (hits != null && hits.Length > 0)
                    {
                        System.Array.Sort(hits, (a,b)=> a.distance.CompareTo(b.distance));
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var col = hits[i].collider;
                            if (col == null) continue;
                            if (col.TryGetComponent<IDamageable>(out var damageable))
                            {
                                if (IsValidDamageTarget(damageable))
                                {
                                    Systems.DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
                                    Despawn();
                                    return;
                                }
                            }
                        }
                    }
                }
                transform.position = endPos;
                // Post-move overlap fallback (handles very small or newly spawned enemies between casts)
                var overlaps = Physics2D.OverlapCircleAll(endPos, sweepRadius * 0.9f);
                if (overlaps != null)
                {
                    for (int i = 0; i < overlaps.Length; i++)
                    {
                        var o = overlaps[i];
                        if (o != null && o.TryGetComponent<IDamageable>(out var dmg2))
                        {
                            if (IsValidDamageTarget(dmg2))
                            {
                                Systems.DamageSystem.Instance.ApplyDamage(dmg2, _damage, _damageType);
                                Despawn();
                                return;
                            }
                        }
                    }
                }
            }
            else
            {
                transform.position = endPos;
            }

            _lifeTimer -= dt;
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
            if (IsValidDamageTarget(damageable))
            {
                Systems.DamageSystem.Instance.ApplyDamage(damageable, _damage, _damageType);
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
                gameObject.SetActive(false);
            }
        }

        // (Viewport culling helper removed)
        /// <summary>
        /// Filters what this (player) projectile is allowed to damage to prevent friendly fire.
        /// Currently only enemies should be damaged; fortress walls and core must be ignored.
        /// </summary>
        private bool IsValidDamageTarget(IDamageable damageable)
        {
            // Only damage enemies (EnemyController implements IDamageable) – skip BaseCore, FortressWall, etc.
            return damageable is EnemyController;
        }
    }
}
