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
    [Header("Ranged")]
    [SerializeField] private Transform muzzle;

    private float _shootCooldown;

        private EnemyData _data;
        private float _currentHealth;
        private string _poolId;
        private bool _released;
        private float _baseMoveSpeed;
        private float _speedMultiplier = 1f;
        private float _slowTimer;
    private float _rangedCooldownTimer;

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
            _rangedCooldownTimer = 0f;

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

            // Move left towards the base
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

            // Try to shoot if configured
            TryShootAtTarget();
        }

        private void TryShootAtTarget()
        {
            if (_data == null || !_data.CanShoot)
            {
                return;
            }

            _rangedCooldownTimer -= Time.deltaTime;
            if (_rangedCooldownTimer > 0f)
            {
                return;
            }

            // Acquire a target: prefer closest wall in front, otherwise the core if in range
            IDamageable target = AcquireTargetInRange(_data.RangedRange);
            if (target == null)
            {
                return;
            }

            Vector3 shootOrigin = muzzle != null ? muzzle.position : transform.position;
            Vector3 targetPos = (target as Component) != null ? ((Component)target).transform.position : shootOrigin + Vector3.left;
            Vector3 dir = (targetPos - shootOrigin);
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            SpawnEnemyProjectile(shootOrigin, dir.normalized);
            _rangedCooldownTimer = Mathf.Max(0.05f, 1f / Mathf.Max(0.01f, _data.RangedFireRate));
        }

        private IDamageable AcquireTargetInRange(float range)
        {
            float rangeSq = range * range;
            IDamageable best = null;
            float bestDist = float.MaxValue;

            // Check core
            if (BaseCore.Instance != null)
            {
                float d = (BaseCore.Instance.transform.position - transform.position).sqrMagnitude;
                if (d <= rangeSq && d < bestDist)
                {
                    best = BaseCore.Instance;
                    bestDist = d;
                }
            }

            // Check walls registered in FortressManager (if available)
            var fm = BulletHeavenFortressDefense.Fortress.FortressManager.HasInstance ? BulletHeavenFortressDefense.Fortress.FortressManager.Instance : null;
            if (fm != null)
            {
                var walls = fm.GetActiveWalls();
                if (walls != null)
                {
                    foreach (var wall in walls)
                    {
                        if (wall == null || !wall.IsAlive)
                        {
                            continue;
                        }
                        float d = (wall.transform.position - transform.position).sqrMagnitude;
                        if (d <= rangeSq && d < bestDist)
                        {
                            best = wall;
                            bestDist = d;
                        }
                    }
                }
            }

            return best;
        }

        private void SpawnEnemyProjectile(Vector3 position, Vector3 direction)
        {
            GameObject projectileObj = null;
            if (!string.IsNullOrEmpty(_data.ProjectilePoolId) && ObjectPoolManager.HasInstance)
            {
                projectileObj = ObjectPoolManager.Instance.Spawn(_data.ProjectilePoolId, position, Quaternion.identity);
            }

            if (projectileObj == null && _data.ProjectilePrefab != null)
            {
                projectileObj = Instantiate(_data.ProjectilePrefab, position, Quaternion.identity);
            }

            if (projectileObj != null)
            {
                if (projectileObj.TryGetComponent<EnemyProjectile>(out var proj))
                {
                    proj.Initialize(_data.RangedDamage, _data.RangedDamageType, direction, _data.ProjectileSpeed, _data.ProjectilePoolId);
                }
                else if (projectileObj.TryGetComponent<Projectile>(out var towerProj))
                {
                    // Fallback: reuse tower projectile logic but with enemy data approximations
                    // Tower projectile expects TowerData; emulate via damage system on hit
                    // Here we simply set its forward and rely on default damage (not ideal). Better to use EnemyProjectile.
                    towerProj.transform.right = direction;
                }
                return;
            }

            // Final fallback: create a simple bullet
            var go = new GameObject("EnemyBullet");
            go.transform.position = position;
            var ep = go.AddComponent<EnemyProjectile>();
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            // lightweight visual
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSolidSprite(new Color(0.9f, 0.2f, 0.2f, 1f));
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 0.1f;
            ep.Initialize(_data.RangedDamage, _data.RangedDamageType, direction, _data.ProjectileSpeed, _data.ProjectilePoolId);
        }

        private Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
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

            if (other.TryGetComponent<BulletHeavenFortressDefense.Fortress.FortressWall>(out var wall))
            {
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                wall.TakeDamageAtPoint(contactDamage, DamageType.Physical, hitPoint);
                Despawn();
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(contactDamage, DamageType.Physical);
                Despawn();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsAlive)
            {
                return;
            }

            if (collision.collider == null) return;
            var col = collision.collider;
            if (col.TryGetComponent<BulletHeavenFortressDefense.Fortress.FortressWall>(out var wall))
            {
                Vector2 hitPoint = collision.GetContact(0).point;
                wall.TakeDamageAtPoint(contactDamage, DamageType.Physical, hitPoint);
                Despawn();
                return;
            }

            if (col.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(contactDamage, DamageType.Physical);
                Despawn();
            }
        }
    }
}
