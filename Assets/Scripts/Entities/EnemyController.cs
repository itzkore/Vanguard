using System;
using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Projectiles; // EnemyProjectile

namespace BulletHeavenFortressDefense.Entities
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        // --- Blood FX Settings ---
        // Allow disabling fallback particle generation if visual artifacts (red squares) are undesired.
        public static bool EnableFallbackBloodFx = true;
        private static readonly List<EnemyController> _activeEnemies = new();
        public static event Action<EnemyController> EnemyDefeated;
        public event Action<float, float> HealthChanged;

        [SerializeField] private Rigidbody2D body;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField, Tooltip("Global multiplier applied to enemy base move speed.")] private float speedGlobalMultiplier = 2.5f;
        [SerializeField] private float contactDamage = 10f;
    [Header("Bounds")]
    [SerializeField, Tooltip("If true, prevent enemies from moving off the left side of the camera viewport.")] private bool clampLeftToViewport = true;
    [SerializeField, Tooltip("Padding added to the left viewport edge when clamping.")] private float leftViewportPadding = 0.05f;
    [Header("Ranged")]
    [SerializeField] private Transform muzzle;
    [SerializeField, Tooltip("Multiplier applied to ranged projectile damage to tune difficulty/balance")] private float projectileDamageMultiplier = 0.4f;
    [Header("Navigation")]
    [SerializeField, Tooltip("If a wall is detected within this distance in front, stop moving and shoot it.")] private float stopDistanceToWall = 0.35f;
    [SerializeField, Tooltip("If true, enemy will steer vertically toward the nearest wall (or core) instead of moving strictly left.")] private bool steerToNearestWall = true;
    [SerializeField, Range(0f,1f), Tooltip("Blend factor between straight-left and direction-to-target.")] private float steerWeight = 0.92f;
    [SerializeField, Tooltip("Within this vertical difference to target, steer is suppressed to avoid jitter.")] private float yAlignEpsilon = 0.03f;
    [SerializeField, Tooltip("Degrees per second the enemy can rotate horizontal direction vector towards target.")] private float turnRateDegreesPerSecond = 720f;
    [Header("Melee")]
    [SerializeField, Tooltip("Distance at which the enemy switches to melee (stop and attack)")] private float meleeRange = 0.45f;
    [SerializeField, Tooltip("Damage multiplier applied to ranged damage when in melee (e.g., 1.5 = +50%)")] private float meleeDamageMult = 1.5f;
    [SerializeField, Tooltip("If true, enemy will not shoot while within melee range of a valid target")] private bool disableRangedInMelee = true;
    [SerializeField, Tooltip("Extra buffer added to melee range for exiting melee (hysteresis) to avoid flicker")]
    private float meleeExitBuffer = 0.1f;
    [SerializeField, Tooltip("Multiplier applied to meleeRange when evaluating distance to the Core only. Fixes cases where enemies appear to reach core but stand just outside base meleeRange due to pivot/collider offsets.")]
    private float coreMeleeRangeMultiplier = 2.2f;

    private float _shootCooldown;

        private EnemyData _data;
        private float _currentHealth;
        private string _poolId;
        private bool _released;
        private float _baseMoveSpeed;
        private float _speedMultiplier = 1f;
        private float _slowTimer;
    private float _rangedCooldownTimer;
    private float _meleeCooldownTimer;
    private IDamageable _meleeTarget;
    private bool _inMeleeState;
    private float _spawnGraceTimer;
    private Vector2 _moveDir = Vector2.left;
    private bool _leftClampedThisFrame;
    private float _maxHealthOverride = -1f; // balance override (two-hit rapid tower rule + wave scaling)
    // Blood FX runtime state
    private float _lastHitFxTime = -999f;
    private static GameObject _fallbackHitFxPrefab;   // lazily built simple particle system if no prefab provided
    private static GameObject _fallbackDeathFxPrefab; // larger burst variant

        public static IReadOnlyList<EnemyController> ActiveEnemies => _activeEnemies;
        public bool IsAlive => _currentHealth > 0f;
        public Vector3 Position => transform.position;
    public Vector3 Velocity { get; private set; }
        public float RemainingHealth => _currentHealth;
        public float MaxHealth => _maxHealthOverride > 0f ? _maxHealthOverride : (_data != null ? _data.Health : Mathf.Max(1f, _currentHealth));
        public float DistanceToBaseSquared => BaseCore.Instance != null
            ? (transform.position - BaseCore.Instance.transform.position).sqrMagnitude
            : float.MaxValue;

        // Slow status exposure for coverage-aware towers (e.g., Slow Tower auto-aim algorithm)
        public bool IsSlowed => _slowTimer > 0f && _speedMultiplier < 0.999f;
        public float RemainingSlowTime => Mathf.Max(0f, _slowTimer);

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
            // Rapid tower two-hit + wave scaling applied later via ApplyBalanceOverrides

            _baseMoveSpeed = (data?.MoveSpeed ?? moveSpeed) * Mathf.Max(0.1f, speedGlobalMultiplier);
            moveSpeed = _baseMoveSpeed;
            contactDamage = data != null ? Mathf.Max(0f, data.ContactDamage) : contactDamage;
            _speedMultiplier = 1f;
            _slowTimer = 0f;
            _rangedCooldownTimer = 0f;
            _spawnGraceTimer = 0.25f; // brief immunity to contact/collision on spawn
            _moveDir = Vector2.left; // Initialize move direction
            // If muzzle wasn't assigned on prefab, try a common child name
            if (muzzle == null)
            {
                var t = transform.Find("Muzzle");
                if (t != null) muzzle = t;
            }

            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            else
            {
                // Ensure a Rigidbody2D exists for proper collisions with non-trigger towers/walls
                body = gameObject.GetComponent<Rigidbody2D>();
                if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            // Ensure a non-trigger Collider2D for physical blocking
            var ownCol = GetComponent<Collider2D>();
            if (ownCol != null)
            {
                ownCol.isTrigger = false;
            }

            // Ensure visuals are visible, above walls, and on Z=0 to avoid spawn flicker
            var pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    r.enabled = true;
                    var c = r.color; c.a = 1f; r.color = c;
                    if (r.sortingOrder < 2) r.sortingOrder = 2; // render above walls (0) and core (1)
                }
            }

            // Ensure tiny health bar widget exists and is synced
            EnsureHealthBarWidget();
            // Reset health bar visibility state for pooled reuse (hide until damaged again)
            var hb = GetComponentInChildren<UI.EnemyHealthBar>(true);
            if (hb != null)
            {
                hb.ResetForSpawn();
            }
            NotifyHealthChanged();
            _lastPos = transform.position;
            Velocity = Vector3.zero;
        }

        private Vector3 _lastPos;

        // Called by WaveManager right after spawning to enforce HP = RapidBaseHitFactor * rapidLevel1Damage * waveMultiplier
        public void ApplyBalanceOverrides(float rapidLevel1Damage, int waveNumber)
        {
            if (rapidLevel1Damage <= 0f) return;
            float baseFactor = Balance.EnemyDynamicBalance.RapidBaseHitFactor; // default 2
            float hp = rapidLevel1Damage * baseFactor;
            float waveMult = BulletHeavenFortressDefense.Balance.BalanceConfig.GetEnemyHpMultiplierForWave(waveNumber);
            hp *= waveMult;
            _maxHealthOverride = hp;
            _currentHealth = hp;
            NotifyHealthChanged();
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            _leftClampedThisFrame = false;

            // Timers that should track real world gameplay (not slowed) use raw deltaTime
            if (_spawnGraceTimer > 0f)
            {
                _spawnGraceTimer -= Time.deltaTime;
            }

            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime; // keep slow effect duration independent of enemy pace multiplier
                if (_slowTimer <= 0f)
                {
                    _speedMultiplier = 1f;
                }
            }

            // Fortress status to drive target priorities
            var fmForBreach = FortressManager.HasInstance ? FortressManager.Instance : null;
            bool coreBreachable = fmForBreach != null && fmForBreach.IsCoreBreachable;

            // Move left towards the base, but stop if a wall is directly ahead within a small distance
            // If core is breachable (middle wall down or at least two walls destroyed), don't stop for walls (head for core through gaps)
            bool wallAhead = !coreBreachable && IsWallAhead(stopDistanceToWall);
            bool inMelee = EvaluateMeleeState(out _meleeTarget);
            if (!wallAhead && !inMelee)
            {
                float speed = _baseMoveSpeed * Mathf.Max(0.05f, _speedMultiplier);
                Vector2 desiredDir = Vector2.left;
                if (steerToNearestWall)
                {
                    Vector3 target = FindSteerTargetPosition(coreBreachable);
                    // If core is breachable and we get clamped on the left, search for a Y that clears LOS to core
                    if (coreBreachable && _leftClampedThisFrame && BaseCore.Instance != null)
                    {
                        float? losY = FindLosYToCore(transform.position.y, 2.0f, 0.1f);
                        if (losY.HasValue)
                        {
                            target = new Vector3(BaseCore.Instance.transform.position.x, losY.Value, target.z);
                        }
                    }

                    Vector2 toTarget = (Vector2)(target - transform.position);
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        float dy = Mathf.Abs(target.y - transform.position.y);
                        float tol = _leftClampedThisFrame ? 0f : yAlignEpsilon;
                        if (dy > tol)
                        {
                            toTarget.Normalize();
                            desiredDir = Vector2.Lerp(Vector2.left, toTarget, Mathf.Clamp01(steerWeight)).normalized;
                        }
                    }
                }

                // Turn _moveDir toward desiredDir with a turn-rate limit
                float enemyDelta = EnemyPace.EnemyDeltaTime; // scaled delta for movement & attack cadence
                float maxRadians = Mathf.Deg2Rad * turnRateDegreesPerSecond * enemyDelta;
                float deltaAngle = Mathf.Deg2Rad * Mathf.Clamp(Vector2.SignedAngle(_moveDir, desiredDir), -Mathf.Rad2Deg * maxRadians, Mathf.Rad2Deg * maxRadians);
                _moveDir = (Quaternion.Euler(0f, 0f, deltaAngle * Mathf.Rad2Deg) * _moveDir).normalized;
                // Ensure we never drift rightwards (prevent bypassing playfield to the right)
                if (_moveDir.x > -0.05f) _moveDir.x = -0.05f;
                _moveDir.Normalize();

                var newPos = (Vector3)(_moveDir * speed * enemyDelta) + transform.position;

                // Prevent moving off the left edge of the viewport
                if (clampLeftToViewport && Camera.main != null)
                {
                    var cam = Camera.main;
                    float distZ = Mathf.Abs(transform.position.z - cam.transform.position.z);
                    float leftX = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, distZ)).x + leftViewportPadding;
                    if (newPos.x < leftX)
                    {
                        newPos.x = leftX;
                        _leftClampedThisFrame = true;
                        if (_moveDir.x < 0f) _moveDir.x = Mathf.Max(_moveDir.x, 0f);
                        _moveDir.Normalize();
                    }
                }

                if (body != null)
                {
                    body.MovePosition(newPos);
                }
                else
                {
                    transform.position = newPos;
                }
            }

            // If in melee range, perform melee attacks; otherwise try ranged
            if (inMelee)
            {
                TryMeleeAttack(_meleeTarget);
            }
            else
            {
                TryShootAtTarget();
            }

            // Update velocity after movement and potential teleports this frame
            Vector3 currentPos = transform.position;
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.0001f;
            Velocity = (currentPos - _lastPos) / dt;
            _lastPos = currentPos;
        }

        private void TryShootAtTarget()
        {
            if (_data == null || !_data.CanShoot)
            {
                return;
            }

            // Optionally suppress ranged when within melee range
            if (disableRangedInMelee && EvaluateMeleeState(out _))
            {
                return;
            }

            _rangedCooldownTimer -= EnemyPace.EnemyDeltaTime;
            if (_rangedCooldownTimer > 0f)
            {
                return;
            }

            // Acquire a target: shoot only a wall that actually blocks LOS to core; otherwise shoot core if in range
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
            // Clamp minimal cooldown so enemies don't create a solid line of bullets
            _rangedCooldownTimer = Mathf.Max(0.15f, 1f / Mathf.Max(0.01f, _data.RangedFireRate));
        }

        private bool IsTargetInMeleeRange(float range, out IDamageable target)
        {
            target = null;
            float rangeSq = range * range;

            // Prefer walls in front first
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm != null)
            {
                var walls = fm.GetActiveWalls();
                if (walls != null)
                {
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var wall = walls[i];
                        if (wall == null || !wall.IsAlive) continue;
                        if (wall.transform.position.x > transform.position.x) continue; // only ahead
                        float d = (wall.transform.position - transform.position).sqrMagnitude;
                        if (d <= rangeSq)
                        {
                            target = wall;
                            return true;
                        }
                    }
                }
            }

            // Otherwise the core
            if (BaseCore.Instance != null)
            {
                // Allow an extended effective range just for the core; this helps when enemy/core pivots or the left viewport clamp keep a tiny gap.
                float coreRange = Mathf.Max(range, range * Mathf.Max(1f, coreMeleeRangeMultiplier));
                float coreRangeSq = coreRange * coreRange;
                float d = (BaseCore.Instance.transform.position - transform.position).sqrMagnitude;
                if (d <= coreRangeSq)
                {
                    target = BaseCore.Instance;
                    return true;
                }
            }
            return false;
        }

        private bool EvaluateMeleeState(out IDamageable target)
        {
            // Enter melee when within meleeRange
            float enterRange = Mathf.Max(0.05f, meleeRange);
            // Exit melee only when beyond meleeRange + buffer
            float exitRange = enterRange + Mathf.Max(0f, meleeExitBuffer);

            // If currently in melee, use the wider exit range to stay until clearly out
            float checkRange = _inMeleeState ? exitRange : enterRange;
            bool nowInMelee = IsTargetInMeleeRange(checkRange, out target);
            _inMeleeState = nowInMelee;
            return nowInMelee;
        }

        private void TryMeleeAttack(IDamageable target)
        {
            if (target == null)
            {
                return;
            }

            _meleeCooldownTimer -= EnemyPace.EnemyDeltaTime;
            if (_meleeCooldownTimer > 0f)
            {
                return;
            }

            float meleeDamage = (_data != null ? _data.RangedDamage : 5f) * Mathf.Max(0.1f, meleeDamageMult);
            if (target is FortressWall wall)
            {
                Vector2 hitPoint = wall.GetComponent<Collider2D>() != null
                    ? (Vector2)wall.GetComponent<Collider2D>().ClosestPoint(transform.position)
                    : (Vector2)wall.transform.position;
                wall.TakeDamageAtPoint(meleeDamage, _data != null ? _data.RangedDamageType : DamageType.Physical, hitPoint);
            }
            else
            {
                target.TakeDamage(meleeDamage, _data != null ? _data.RangedDamageType : DamageType.Physical);
            }

            // Reuse ranged fire rate cadence for melee
            float fireCooldown = Mathf.Max(0.15f, 1f / Mathf.Max(0.01f, _data != null ? _data.RangedFireRate : 1f));
            _meleeCooldownTimer = fireCooldown;
        }

        private IDamageable AcquireTargetInRange(float range)
        {
            float maxDist = Mathf.Max(0.01f, range);
            Vector3 origin = (muzzle != null ? muzzle.position : transform.position);
            var fmForBreach = FortressManager.HasInstance ? FortressManager.Instance : null;
            bool coreBreachable = fmForBreach != null && fmForBreach.IsCoreBreachable;

            if (BaseCore.Instance == null)
            {
                return null;
            }

            if (coreBreachable)
            {
                // Breach open → try to shoot core if within range and LOS not blocked
                Vector3 corePos = BaseCore.Instance.transform.position;
                Vector2 dir = (corePos - origin);
                float distToCore = dir.magnitude;
                if (distToCore < 0.0001f)
                {
                    return BaseCore.Instance;
                }
                dir /= distToCore;

                if (distToCore <= maxDist)
                {
                    var losHits = Physics2D.RaycastAll(origin, dir, distToCore);
                    for (int i = 0; i < losHits.Length; i++)
                    {
                        var h = losHits[i];
                        if (h.collider == null) continue;
                        if (h.collider.isTrigger) continue;
                        var w = h.collider.GetComponent<Fortress.FortressWall>();
                        if (w != null && w.IsAlive)
                        {
                            return null; // blocked by alive wall; don't shoot
                        }
                    }
                    return BaseCore.Instance;
                }
                return null;
            }

            // Breach not open → target the weakest wall in range
            int bestHP = int.MaxValue;
            float bestDist2 = float.MaxValue;
            Fortress.FortressWall bestWall = null;
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm != null)
            {
                var walls = fm.GetActiveWalls();
                if (walls != null)
                {
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var w = walls[i];
                        if (w == null || !w.IsAlive) continue;
                        float d2 = (w.transform.position - transform.position).sqrMagnitude;
                        if (d2 <= maxDist * maxDist)
                        {
                            int hp = w.CurrentHealth;
                            if (hp < bestHP || (hp == bestHP && d2 < bestDist2))
                            {
                                bestHP = hp;
                                bestDist2 = d2;
                                bestWall = w;
                            }
                        }
                    }
                }
            }

            return bestWall;
        }

    private bool IsWallAhead(float distance)
        {
            // Raycast a short distance to the left to detect a non-trigger FortressWall directly ahead
            Vector2 origin = transform.position;
            var hit = Physics2D.Raycast(origin, Vector2.left, Mathf.Max(0.01f, distance));
            if (hit.collider == null) return false;
            if (hit.collider.isTrigger) return false;
            return hit.collider.GetComponent<FortressWall>() != null;
        }

        private Vector3 FindSteerTargetPosition(bool coreBreachable)
        {
            // If breach is open, steer toward the core (use gaps)
            if (coreBreachable && BaseCore.Instance != null)
            {
                return BaseCore.Instance.transform.position;
            }

            // Breach not open → steer toward the weakest wall
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            Vector3 self = transform.position;
            int bestHP = int.MaxValue;
            float bestDist = float.MaxValue;
            Fortress.FortressWall bestWall = null;
            if (fm != null)
            {
                var walls = fm.GetActiveWalls();
                if (walls != null)
                {
                    for (int i = 0; i < walls.Count; i++)
                    {
                        var w = walls[i];
                        if (w == null || !w.IsAlive) continue;
                        int hp = w.CurrentHealth;
                        float d = (w.transform.position - self).sqrMagnitude;
                        if (hp < bestHP || (hp == bestHP && d < bestDist))
                        {
                            bestHP = hp;
                            bestDist = d;
                            bestWall = w;
                        }
                    }
                }
            }

            if (bestWall != null)
            {
                return bestWall.transform.position;
            }

            if (BaseCore.Instance != null)
            {
                return BaseCore.Instance.transform.position;
            }

            return self + Vector3.left; // fallback
        }

        // Scan vertically above/below current Y to find a line of sight to the core, returning the first Y that clears LOS
        private float? FindLosYToCore(float startY, float scanHeight, float step)
        {
            if (BaseCore.Instance == null) return null;
            Vector3 core = BaseCore.Instance.transform.position;
            Vector3 origin = transform.position;
            float half = Mathf.Max(0.05f, scanHeight * 0.5f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(half / Mathf.Max(0.02f, step)));
            // alternate up/down sampling from the center line
            for (int i = 0; i <= steps; i++)
            {
                float offset = i * step;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float y = startY + offset * sign;
                    if (y < startY - half || y > startY + half) continue;
                    Vector3 testOrigin = new Vector3(origin.x, y, origin.z);
                    Vector2 dir = (core - testOrigin);
                    float dist = dir.magnitude;
                    if (dist < 0.0001f) return y;
                    dir /= dist;
                    var hits = Physics2D.RaycastAll(testOrigin, dir, dist);
                    bool blocked = false;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        var col = hits[h].collider;
                        if (col == null || col.isTrigger) continue;
                        var w = col.GetComponent<Fortress.FortressWall>();
                        if (w != null && w.IsAlive)
                        {
                            blocked = true; break;
                        }
                    }
                    if (!blocked)
                    {
                        return y;
                    }
                }
            }
            return null;
        }

        private bool AreAllWallsAlive()
        {
            var fm = FortressManager.HasInstance ? FortressManager.Instance : null;
            if (fm == null) return true;
            var walls = fm.GetActiveWalls();
            if (walls == null || walls.Count == 0) return true;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (w == null) continue;
                if (!w.IsAlive) return false;
            }
            return true;
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
                // Prefer EnemyProjectile; if prefab only has tower Projectile, add EnemyProjectile and disable tower one
                if (!projectileObj.TryGetComponent<EnemyProjectile>(out var proj))
                {
                    if (projectileObj.TryGetComponent<Projectile>(out var towerProj))
                    {
                        towerProj.enabled = false;
                    }
                    proj = projectileObj.AddComponent<EnemyProjectile>();
                }
                // Try to mirror Rapid Tower projectile parameters if available on the prefab
                float speed = Mathf.Max(6f, _data.ProjectileSpeed);
                float maxLifetime = 5f;
                var rapidProjTemplate = projectileObj.GetComponentInChildren<Projectile>(true);
                if (rapidProjTemplate != null)
                {
                    speed = Mathf.Max(6f, rapidProjTemplate.Speed);
                    maxLifetime = Mathf.Max(0.1f, rapidProjTemplate.MaxLifetime);
                    // Keep exact prefab visual size by setting target width to measured sprite width
                    var sr2 = projectileObj.GetComponentInChildren<SpriteRenderer>();
                    if (sr2 != null)
                    {
                        // Clamp mirrored width to a small maximum to avoid giant enemy bullets even if prefab sprite is large
                        float worldWidth = Mathf.Max(0.001f, sr2.bounds.size.x);
                        float maxEnemyWidth = 0.03f; // increased by ~50% for better readability
                        float clamped = Mathf.Min(worldWidth, maxEnemyWidth);
                        proj.SetAutoScale(true);
                        proj.SetTargetWorldWidth(clamped);
                    }
                }
                proj.SetMaxLifetime(maxLifetime);
                float rangedDmg = (_data != null ? _data.RangedDamage : 5f) * Mathf.Max(0.01f, projectileDamageMultiplier);
                proj.Initialize(rangedDmg, _data.RangedDamageType, direction, speed, _data.ProjectilePoolId);
                return;
            }

            // Final fallback: create a simple bullet
            var go = new GameObject("EnemyBullet");
            go.transform.position = position;
            // Add collider first to satisfy EnemyProjectile's RequireComponent
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            var ep = go.AddComponent<EnemyProjectile>();
            // lightweight visual
            var fallbackSr = go.AddComponent<SpriteRenderer>();
            fallbackSr.sprite = CreateSolidSprite(new Color(0.9f, 0.2f, 0.2f, 1f));
            fallbackSr.sortingOrder = 2;
            // Keep tiny by renderer autoscale in EnemyProjectile
            // Try to mirror Rapid Tower projectile parameters if TestProjectile has a Projectile component
            float fallbackSpeed = Mathf.Max(6f, _data.ProjectileSpeed);
            float fallbackLifetime = 5f;
            if (_data.ProjectilePrefab != null)
            {
                var template = _data.ProjectilePrefab.GetComponentInChildren<Projectile>(true);
                if (template != null)
                {
                    fallbackSpeed = Mathf.Max(6f, template.Speed);
                    fallbackLifetime = Mathf.Max(0.1f, template.MaxLifetime);
                }
            }
            ep.SetMaxLifetime(fallbackLifetime);
            float fallbackDmg = (_data != null ? _data.RangedDamage : 5f) * Mathf.Max(0.01f, projectileDamageMultiplier);
            ep.Initialize(fallbackDmg, _data.RangedDamageType, direction, fallbackSpeed, _data.ProjectilePoolId);
        }

        private Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        public void TakeDamage(float amount, DamageType damageType)
        {
            if (!IsAlive)
            {
                return;
            }

            float modifier = _data != null ? _data.GetResistanceModifier(damageType) : 1f;
            _currentHealth -= amount * modifier;
            if (_currentHealth < 0f) _currentHealth = 0f;
            NotifyHealthChanged();

            // Spawn per‑hit blood (throttled) if still alive after damage
            if (_currentHealth > 0f)
            {
                TrySpawnHitBloodFx();
            }

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
            if (Systems.EconomySystem.HasInstance)
            {
                Systems.EconomySystem.Instance.AddKillReward(_data != null ? _data.Reward : 0);
            }
            // Death blood burst (use larger FX)
            SpawnDeathBloodFx();
            // Notify listeners that this enemy has been defeated
            try { EnemyDefeated?.Invoke(this); } catch { }
            Despawn();
        }
        private void Despawn()
        {
            if (_released) return;
            _released = true;
            _currentHealth = 0f;
            NotifyHealthChanged();

            if (!string.IsNullOrEmpty(_poolId) && ObjectPoolManager.HasInstance)
            {
                ObjectPoolManager.Instance.Release(_poolId, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject, 10f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other) { /* Contact damage disabled; melee handles damage */ }

        private void OnCollisionEnter2D(Collision2D collision) { /* Contact damage disabled; melee handles damage */ }

        private void EnsureHealthBarWidget()
        {
            // Look for existing widget
            var existing = GetComponentInChildren<UI.EnemyHealthBar>(true);
            if (existing != null)
            {
                return;
            }
            var go = new GameObject("EnemyHP", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            // Slightly above the enemy
            go.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            go.AddComponent<UI.EnemyHealthBar>();
        }

        private void NotifyHealthChanged()
        {
            try { HealthChanged?.Invoke(_currentHealth, MaxHealth); } catch { }
        }

        #region Blood FX
        private void TrySpawnHitBloodFx()
        {
            if (_data == null) return;
            float cd = _data.HitBloodCooldown;
            if (cd > 0f && Time.time < _lastHitFxTime + cd) return; // throttle
            _lastHitFxTime = Time.time;
            SpawnBloodFx(isDeath:false);
        }

        private void SpawnDeathBloodFx()
        {
            SpawnBloodFx(isDeath:true);
        }

        private void SpawnBloodFx(bool isDeath)
        {
            // Decide which prefab to use
            GameObject prefab = null;
            if (_data != null)
            {
                if (isDeath && _data.DeathBloodPrefab != null) prefab = _data.DeathBloodPrefab;
                else if (!isDeath && _data.HitBloodPrefab != null) prefab = _data.HitBloodPrefab;
            }

            if (prefab == null)
            {
                // Build (once) a tiny fallback particle prefabs
                EnsureFallbackBloodPrefabs();
                prefab = isDeath ? _fallbackDeathFxPrefab : _fallbackHitFxPrefab;
            }

            if (prefab == null) return; // still nothing

            Vector3 pos = transform.position + new Vector3(0f, 0.1f, 0f); // slight vertical offset
            Quaternion rot = Quaternion.identity;
            if (_data != null && _data.OrientBloodToHit)
            {
                // Approximate incoming direction as from player's side (positive X) → rotate so particle forward (right) points that way.
                // If later we add true hit direction we can replace this heuristic.
                Vector3 dir = Vector3.right; // heuristic assumption
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir.Normalize();
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    rot = Quaternion.Euler(0f, 0f, angle + UnityEngine.Random.Range(-12f,12f));
                }
            }

            // If enemy fully off-screen, skip FX to avoid odd appearing-at-center issues (camera effects / canvas).
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(pos);
                bool off = vp.z < 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f;
                if (off)
                {
                    return; // silently skip
                }
            }

            GameObject fxInstance = null;
            fxInstance = Instantiate(prefab, pos, rot);
            if (!fxInstance.activeSelf) fxInstance.SetActive(true);

            // Auto-destroy after lifetime if it has a particle system
            var ps = fxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                float life = main.startLifetime.constantMax + main.duration + 0.25f;
                Destroy(fxInstance, life);
                ps.Play();
            }
            else
            {
                Destroy(fxInstance, isDeath ? 2f : 1f);
            }
        }

        private static void EnsureFallbackBloodPrefabs()
        {
            if (!EnableFallbackBloodFx) return; // skip building any fallback FX
            if (_fallbackHitFxPrefab != null && _fallbackDeathFxPrefab != null) return;

            // Shared material (Sprites/Default)
            Material spriteMat = new Material(Shader.Find("Sprites/Default"));

            if (_fallbackHitFxPrefab == null)
            {
                _fallbackHitFxPrefab = BuildSimpleBloodFx("BloodHit_Fallback", 12, 0.35f, 1.6f, new Color(0.75f,0f,0f,1f), spriteMat);
            }
            if (_fallbackDeathFxPrefab == null)
            {
                _fallbackDeathFxPrefab = BuildSimpleBloodFx("BloodDeath_Fallback", 28, 0.55f, 2.2f, new Color(0.9f,0f,0f,1f), spriteMat);
            }
        }

        private static GameObject BuildSimpleBloodFx(string name, int particleCount, float lifetime, float speed, Color color, Material mat)
        {
            var go = new GameObject(name);
            // Keep template far offscreen but active so Instantiate produces active clone.
            go.transform.position = new Vector3(9999f, 9999f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = particleCount;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = color;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new [] { new ParticleSystem.Burst(0f, (short)particleCount) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new [] { new GradientColorKey(color,0f), new GradientColorKey(new Color(color.r*0.6f,color.g*0.1f,color.b*0.1f,1f),1f)},
                new [] { new GradientAlphaKey(1f,0f), new GradientAlphaKey(0f,1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.sortingOrder = 10; // above enemy sprite
            // Ensure no auto-play and clear emission until explicitly played on instance.
            main.playOnAwake = false;
            var emission2 = ps.emission;
            emission2.enabled = true; // keep burst definition
            return go;
        }
        #endregion
    }
}
