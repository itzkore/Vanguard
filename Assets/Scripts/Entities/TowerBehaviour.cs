using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Projectiles; // SniperProjectile reference
using System.Collections.Generic; // for slow coverage sorting

namespace BulletHeavenFortressDefense.Entities
{
    public class TowerBehaviour : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown;
        [SerializeField, Tooltip("Current level (starts at 1). ")] private int level = 1;
        [SerializeField, Tooltip("Total energy invested in this tower (build + upgrades).")] private int investedEnergy;
        [Header("Predictive Aim")]
        [SerializeField, Tooltip("Enable leading of moving targets.")] private bool usePredictiveAim = true;
        [SerializeField, Tooltip("Maximum seconds ahead to lead.")] private float maxLeadTime = 1.2f;
        [SerializeField, Tooltip("Lead bias multiplier (1=exact; <1 under, >1 over). ")] private float leadBias = 1f;

        private TowerData _data;
        private float _cooldownTimer;
        private float _rangeSquared;
        private EnemyController _currentTarget;
        private FortressMount _mount;
    // Cache of the last projectile speed actually used (after any runtime overrides),
    // so predictive aiming can still function even if TowerData base speed is zero or modified by components.
    private float _lastUsedProjectileSpeed = 0f;

        public FortressMount Mount => _mount;
        public int Level => level;
        public int InvestedEnergy => investedEnergy;
    public TowerData Data => _data; // expose for UI
        // Cached last-calculated values for UI
        public float CurrentFireRate { get; private set; } // shots per second
        public float CurrentRange { get; private set; } // world units
    public float BaseDamage => _data != null ? _data.Damage : 0f; // base damage (per shot)
    public float CurrentDamage { get; private set; }
    public string DisplayName => _data != null ? _data.DisplayName : name;

    // Specialized scaling caches (recomputed on upgrade)
    public float CurrentSplashRadius { get; private set; }
    public float CurrentSlowFactor { get; private set; } // enemy speed multiplier
    public float CurrentSlowDuration { get; private set; }
    public int CurrentProjectilesPerShot { get; private set; } = 1;

        public float GetNextLevelFireRate()
        {
            if (!CanUpgrade()) return CurrentFireRate;
            float fireRate = _data.FireRate;
            float levelsAbove = (level); // next level - 1 above base
            fireRate *= Mathf.Pow(_data.FireRatePerLevelMult, levelsAbove);
            return fireRate;
        }

        public float GetNextLevelDamage()
        {
            if (!CanUpgrade()) return CurrentDamage;
            float dmg = _data.Damage;
            float levelsAbove = (level); // next level - 1 above base
            dmg *= Mathf.Pow(_data.DamagePerLevelMult, levelsAbove);
            dmg += _data.DamageFlatPerLevel * levelsAbove;
            // Fallback automatic scaling if designer left both at neutral values
            if (Mathf.Approximately(_data.DamagePerLevelMult, 1f) && Mathf.Approximately(_data.DamageFlatPerLevel, 0f))
            {
                const float DEFAULT_DAMAGE_GROWTH = 1.12f; // 12% per level baseline
                dmg *= Mathf.Pow(DEFAULT_DAMAGE_GROWTH, levelsAbove);
            }
            return dmg;
        }

        public float GetNextLevelRange()
        {
            if (!CanUpgrade()) return CurrentRange;
            float range = _data.Range;
            float levelsAbove = (level); // next level - 1 above base
            range *= Mathf.Pow(_data.RangePerLevelMult, levelsAbove);
            range *= 3f; // apply same global multiplier
            if (_data.IsSniper || _treatAsSniper)
            {
                range *= _data.SniperRangeMultiplier; // mirror RecalculateStats
            }
            return range;
        }

        public float GetNextLevelSplashRadius()
        {
            if (!CanUpgrade() || _data == null || !_data.IsSplash) return CurrentSplashRadius;
            float levelsAbove = level; // next level - 1 above base
            float r = _data.SplashRadiusBase;
            r *= Mathf.Pow(_data.SplashRadiusPerLevelMult, levelsAbove);
            r += _data.SplashRadiusPerLevelFlat * levelsAbove;
            return r;
        }

        public float GetNextLevelSlowFactor()
        {
            if (!CanUpgrade() || _data == null || !_data.IsSlow) return CurrentSlowFactor;
            float levelsAbove = level; // next level - 1 above base
            float slow = _data.SlowFactorBase + _data.SlowFactorPerLevelAdd * levelsAbove;
            return Mathf.Clamp(slow, 0.05f, 1f);
        }

        public float GetNextLevelSlowDuration()
        {
            if (!CanUpgrade() || _data == null || !_data.IsSlow) return CurrentSlowDuration;
            float levelsAbove = level; // next level - 1 above base
            float dur = _data.SlowDurationBase + _data.SlowDurationPerLevelAdd * levelsAbove;
            return Mathf.Max(0f, dur);
        }

        public int GetNextLevelProjectilesPerShot()
        {
            if (!CanUpgrade() || _data == null || !_data.IsSlow) return CurrentProjectilesPerShot;
            float levelsAbove = level; // next level - 1 above base
            float val = _data.ProjectilesPerShotBase + (levelsAbove * _data.ProjectilesPerShotPerLevel);
            int count = Mathf.Max(1, _data.ProjectilesPerShotBase + Mathf.FloorToInt(levelsAbove * _data.ProjectilesPerShotPerLevel));
            return count;
        }

        public void Initialize(TowerData data)
        {
            _data = data;
            level = Mathf.Max(1, level);
            // Auto-detect sniper classification if not explicitly flagged but projectile prefab contains SniperProjectile
            if (_data != null && !_data.IsSniper && _data.ProjectilePrefab != null)
            {
                if (_data.ProjectilePrefab.GetComponent<BulletHeavenFortressDefense.Projectiles.SniperProjectile>() != null)
                {
                    // We cannot mutate ScriptableObject safely at runtime (would dirty asset), so we branch by local override.
                    _treatAsSniper = true;
                }
            }
            RecalculateStats();
            // Attach star rank display if not present
            if (GetComponent<BulletHeavenFortressDefense.UI.TowerStarRankDisplay>() == null)
            {
                gameObject.AddComponent<BulletHeavenFortressDefense.UI.TowerStarRankDisplay>();
            }
            // Attach simple level number (centered) if not present
            if (GetComponent<BulletHeavenFortressDefense.UI.TowerLevelNumberDisplay>() == null)
            {
                gameObject.AddComponent<BulletHeavenFortressDefense.UI.TowerLevelNumberDisplay>();
            }
        }

        // Local runtime override (when projectile prefab implies sniper behavior but data flag not set)
        private bool _treatAsSniper = false;

        public void AssignMount(FortressMount mount)
        {
            _mount = mount;
        }

        private void OnDestroy()
        {
            if (_mount != null)
            {
                var mountRef = _mount;
                _mount = null;
                mountRef.NotifyTowerDestroyed(this);
            }
        }

        private void Update()
        {
            if (_data == null)
            {
                return;
            }

            AcquireTarget();
            if (_currentTarget == null)
            {
                return;
            }

            if (_data.RotateTowardsTarget && muzzle != null)
            {
                Vector3 lookDirection = (_currentTarget.Position - muzzle.position);
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    muzzle.right = lookDirection.normalized;
                }
            }

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                FireAtTarget();
                _cooldownTimer = fireCooldown;
            }
        }

        public bool CanUpgrade()
        {
            return _data != null && level < _data.MaxLevel;
        }

        // Fired after this tower successfully launches its volley (at least one projectile)
        public event System.Action<TowerBehaviour> Fired;
        // Fired after stats (damage, fire rate, range, splash radius, slow params) are recalculated (initialization & upgrade)
        public event System.Action<TowerBehaviour> StatsRecalculated;

        public int GetNextUpgradeCost()
        {
            if (!CanUpgrade()) return 0;
            // geometric progression: base * growth^(level-1)
            float cost = _data.UpgradeCostBase * Mathf.Pow(_data.UpgradeCostGrowth, level - 1);
            // Apply global upgrade multiplier from EconomySystem
            if (Systems.EconomySystem.HasInstance)
            {
                cost *= Systems.EconomySystem.Instance.UpgradeCostGlobalMult;
            }
            // Apply per-tower extra multiplier (lets us make rapid tower uniquely more expensive)
            cost *= _data.UpgradeCostExtraMult;
            return Mathf.RoundToInt(cost);
        }

        public bool TryUpgrade()
        {
            if (!CanUpgrade()) return false;
            float oldDmg = CurrentDamage;
            float oldFr = CurrentFireRate;
            int cost = GetNextUpgradeCost();
            if (!Systems.EconomySystem.Instance.TrySpend(cost))
            {
                return false;
            }

            level++;
            investedEnergy += cost;
            RecalculateStats();
            if (oldDmg > 0f || oldFr > 0f)
            {
                Debug.Log($"[TowerUpgrade] {_data.DisplayName} L{level - 1}->{level} cost={cost} DMG {oldDmg:F2}->{CurrentDamage:F2} FR {oldFr:F2}->{CurrentFireRate:F2} DPS {(oldDmg*oldFr):F2}->{(CurrentDamage*CurrentFireRate):F2}");
            }
            return true;
        }

        public int GetSellRefund()
        {
            if (_data == null) return 0;
            return Mathf.RoundToInt(investedEnergy * _data.SellRefundPercent);
        }

        public void Sell()
        {
            int refund = GetSellRefund();
            Systems.EconomySystem.Instance.Add(refund);
            if (_mount != null)
            {
                var mountRef = _mount;
                _mount = null;
                mountRef.DestroyMountedTower();
            }
            else
            {
                Managers.TowerManager.Instance?.RemoveTower(this);
            }
        }

        private void RecalculateStats()
        {
            if (_data == null)
            {
                fireCooldown = 1f;
                _rangeSquared = 0f;
                StatsRecalculated?.Invoke(this);
                return;
            }

            float fireRate = _data.FireRate;
            float range = _data.Range;
            float dmgVal = _data.Damage;
            if (level > 1)
            {
                float levelsAbove = level - 1;
                fireRate *= Mathf.Pow(_data.FireRatePerLevelMult, levelsAbove);
                range *= Mathf.Pow(_data.RangePerLevelMult, levelsAbove);
                dmgVal *= Mathf.Pow(_data.DamagePerLevelMult, levelsAbove);
                dmgVal += _data.DamageFlatPerLevel * levelsAbove;
                if (Mathf.Approximately(_data.DamagePerLevelMult, 1f) && Mathf.Approximately(_data.DamageFlatPerLevel, 0f))
                {
                    const float DEFAULT_DAMAGE_GROWTH = 1.12f;
                    dmgVal *= Mathf.Pow(DEFAULT_DAMAGE_GROWTH, levelsAbove);
                }
            }

            // Slow tower global damage downscale after normal scaling
            if (_data.IsSlow)
            {
                dmgVal *= _data.SlowTowerDamageMultiplier;
            }
            // Sniper tower post scaling damage multiplier
            if (_data.IsSniper || _treatAsSniper)
            {
                dmgVal *= _data.SniperDamageMultiplier;
            }

            // Global range multiplier: triple all turret ranges
            range *= 3f;
            // Additional sniper range boost
            if (_data.IsSniper || _treatAsSniper)
            {
                range *= _data.SniperRangeMultiplier;
            }

            fireCooldown = fireRate > 0f ? 1f / fireRate : 1f;
            CurrentFireRate = fireRate;
            CurrentRange = range;
            CurrentDamage = dmgVal;
            _rangeSquared = range * range;

            // Splash radius scaling
            if (_data.IsSplash)
            {
                float levelsAbove = level - 1;
                float r = _data.SplashRadiusBase;
                if (levelsAbove > 0)
                {
                    r *= Mathf.Pow(_data.SplashRadiusPerLevelMult, levelsAbove);
                    r += _data.SplashRadiusPerLevelFlat * levelsAbove;
                }
                CurrentSplashRadius = Mathf.Max(0f, r);
            }
            else
            {
                CurrentSplashRadius = 0f;
            }

            // Slow stats scaling
            if (_data.IsSlow)
            {
                float levelsAbove = level - 1;
                float slowFactor = _data.SlowFactorBase + _data.SlowFactorPerLevelAdd * levelsAbove;
                slowFactor = Mathf.Clamp(slowFactor, 0.05f, 1f);
                float slowDuration = _data.SlowDurationBase + _data.SlowDurationPerLevelAdd * levelsAbove;
                slowDuration = Mathf.Max(0f, slowDuration);
                // Override design: slow tower fires 3 projectiles at level 1, +1 each level
                int projectiles = 3 + Mathf.Max(0, (int)levelsAbove);
                CurrentSlowFactor = slowFactor;
                CurrentSlowDuration = slowDuration;
                CurrentProjectilesPerShot = Mathf.Max(1, projectiles);
            }
            else
            {
                CurrentSlowFactor = 1f;
                CurrentSlowDuration = 0f;
                CurrentProjectilesPerShot = 1;
            }

            StatsRecalculated?.Invoke(this);
        }

        private void AcquireTarget()
        {
            // Release focus if old target invalid now
            if (_currentTarget != null && (!_currentTarget.IsAlive || (_currentTarget.Position - transform.position).sqrMagnitude > _rangeSquared))
            {
                if (AI.TargetFocusCoordinator.HasInstance)
                {
                    AI.TargetFocusCoordinator.Instance.NotifyRelease(_currentTarget);
                }
                _currentTarget = null;
            }

            if (_currentTarget != null) return; // still valid

            // Specialized coverage-driven targeting for Slow Towers: prioritize unslowed enemies first,
            // then those whose slow is about to expire, with a slight bias toward enemies near the edge of range.
            if (_data != null && _data.IsSlow)
            {
                EnemyController best = null;
                float slowCoverageBestScore = float.MaxValue; // lower = better
                foreach (var enemy in EnemyController.ActiveEnemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    float distSq = (enemy.Position - transform.position).sqrMagnitude;
                    if (distSq > _rangeSquared) continue;
                    float score = ComputeSlowCoverageScore(enemy, distSq);
                    // Incorporate a mild focus penalty so multiple slow towers spread debuff more evenly
                    if (AI.TargetFocusCoordinator.HasInstance)
                    {
                        score += AI.TargetFocusCoordinator.Instance.GetPenalty(enemy) * 0.05f;
                    }
                    if (score < slowCoverageBestScore)
                    {
                        slowCoverageBestScore = score;
                        best = enemy;
                    }
                }
                if (best != null)
                {
                    _currentTarget = best;
                    if (AI.TargetFocusCoordinator.HasInstance)
                    {
                        AI.TargetFocusCoordinator.Instance.NotifyFocus(_currentTarget);
                    }
                    return; // done
                }
                // Fallback to generic logic below if none found (unlikely)
            }

            EnemyController bestCandidate = null;
            float bestScore = float.MaxValue; // lower is better
            float bestHealth = float.MinValue;
            float lowestHealth = float.MaxValue;

            foreach (var enemy in EnemyController.ActiveEnemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                float distanceSq = (enemy.Position - transform.position).sqrMagnitude;
                if (distanceSq > _rangeSquared) continue;

                float baseMetric = 0f; // underlying metric before penalty
                switch (_data.TargetPriority)
                {
                    case TargetPriority.ClosestToTower:
                        baseMetric = distanceSq;
                        break;
                    case TargetPriority.ClosestToBase:
                        baseMetric = enemy.DistanceToBaseSquared;
                        break;
                    case TargetPriority.HighestHealth:
                        if (enemy.RemainingHealth > bestHealth)
                        {
                            bestHealth = enemy.RemainingHealth;
                        }
                        // we invert by negating health later
                        baseMetric = -enemy.RemainingHealth;
                        break;
                    case TargetPriority.LowestHealth:
                        if (enemy.RemainingHealth < lowestHealth)
                        {
                            lowestHealth = enemy.RemainingHealth;
                        }
                        baseMetric = enemy.RemainingHealth;
                        break;
                }

                float penalty = 0f;
                if (AI.TargetFocusCoordinator.HasInstance)
                {
                    penalty = AI.TargetFocusCoordinator.Instance.GetPenalty(enemy);
                }
                float finalScore = baseMetric + penalty;
                if (finalScore < bestScore)
                {
                    bestScore = finalScore;
                    bestCandidate = enemy;
                }
            }

            if (bestCandidate != null)
            {
                _currentTarget = bestCandidate;
                if (AI.TargetFocusCoordinator.HasInstance)
                {
                    AI.TargetFocusCoordinator.Instance.NotifyFocus(_currentTarget);
                }
            }
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                return;
            }

            if (muzzle == null)
            {
                return;
            }

            Vector3 direction = usePredictiveAim ? ComputeAimDirection(_currentTarget, muzzle.position) : (_currentTarget.Position - muzzle.position);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            int shots = Mathf.Max(1, CurrentProjectilesPerShot);
            if (_data.IsSlow && shots > 1)
            {
                // Coverage-focused selection: score enemies (unslowed first, then expiring slows, then edge cases)
                List<(EnemyController enemy, float score)> scored = new List<(EnemyController, float)>();
                foreach (var enemy in EnemyController.ActiveEnemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    float distSq = (enemy.Position - transform.position).sqrMagnitude;
                    if (distSq > _rangeSquared) continue;
                    float s = ComputeSlowCoverageScore(enemy, distSq);
                    scored.Add((enemy, s));
                }
                if (scored.Count == 0)
                {
                    scored.Add((_currentTarget, 0f));
                }
                // Sort ascending by score (lower is better)
                scored.Sort((a, b) => a.score.CompareTo(b.score));
                int select = Mathf.Min(shots, scored.Count);
                List<EnemyController> targets = new List<EnemyController>(select);
                for (int i = 0; i < select; i++)
                {
                    targets.Add(scored[i].enemy);
                }
                shots = select;
                StopAllCoroutines();
                StartCoroutine(SlowTowerSequentialFire(targets, 0.08f));
                Fired?.Invoke(this);
            }
            else
            {
                for (int i = 0; i < shots; i++)
                {
                    Vector3 spreadDir = direction;
                    if (shots > 1 && !(_data.IsSniper || _treatAsSniper))
                    {
                        float angleSpread = 10f;
                        float t = shots == 1 ? 0f : (i / (float)(shots - 1));
                        float angle = Mathf.Lerp(-angleSpread, angleSpread, t);
                        spreadDir = Quaternion.Euler(0f, 0f, angle) * direction;
                    }
                    LaunchProjectile(spreadDir);
                }
                if (shots > 0)
                {
                    Fired?.Invoke(this);
                }
            }
        }

        private System.Collections.IEnumerator SlowTowerSequentialFire(System.Collections.Generic.List<EnemyController> targets, float delay)
        {
            int count = targets != null ? targets.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var enemy = targets[i];
                if (enemy != null && enemy.IsAlive)
                {
                    Vector3 dir = usePredictiveAim ? ComputeAimDirection(enemy, muzzle.position) : (enemy.Position - muzzle.position);
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        LaunchProjectile(dir);
                    }
                }
                if (i < count - 1 && delay > 0f)
                {
                    float t = 0f;
                    while (t < delay)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
            }
        }

        // Computes a coverage score for slow distribution; lower score = higher priority.
        // Priorities:
        // 1) Unslowed enemies get a large negative offset to guarantee first pick.
        // 2) Among slowed enemies, those with the least remaining slow time are prioritized.
        // 3) Slight preference to enemies nearer the edge of range (so they are slowed before leaving).
        private float ComputeSlowCoverageScore(EnemyController enemy, float distSq)
        {
            bool unslowed = !enemy.IsSlowed;
            float baseScore = 0f;
            if (unslowed)
            {
                baseScore -= 10000f; // dominate all other factors
            }
            else
            {
                // Remaining slow time scaled: shorter remaining => lower score
                baseScore += enemy.RemainingSlowTime * 100f; // amplify so time dominates over distance subtlety
            }

            // Edge proximity factor (0 center, 1 edge) -> reduce score slightly near edge to apply slow before exiting
            if (_rangeSquared > 0.0001f)
            {
                float edgeProximity = Mathf.Clamp01(distSq / _rangeSquared);
                baseScore -= edgeProximity * 5f; // small influence
            }
            return baseScore;
        }

        private void LaunchProjectile(Vector3 direction)
        {
            GameObject projectileObj = null;
            // 1) Try generic ObjectPoolManager if an explicit pool id is provided.
            if (!string.IsNullOrEmpty(_data.ProjectilePoolId) && ObjectPoolManager.HasInstance)
            {
                projectileObj = ObjectPoolManager.Instance.Spawn(_data.ProjectilePoolId, muzzle.position, Quaternion.identity);
            }
            // 2) Fallback to specialized ProjectilePool (auto bucket on prefab) when no explicit pool id or generic pool returned null.
            if (projectileObj == null && _data.ProjectilePrefab != null && Pooling.ProjectilePool.HasInstance)
            {
                projectileObj = Pooling.ProjectilePool.Get(_data.ProjectilePrefab, muzzle.position, Quaternion.identity);
            }
            // 3) Final fallback: direct Instantiate (keeps legacy behavior if no pool present yet).
            if (projectileObj == null && _data.ProjectilePrefab != null)
            {
                projectileObj = Instantiate(_data.ProjectilePrefab, muzzle.position, Quaternion.identity);
            }

            if (projectileObj != null)
            {
                if (_data.IsSplash && projectileObj.TryGetComponent(out SplashProjectile splashProj))
                {
                    var radiusField = typeof(SplashProjectile).GetField("radius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var falloffField = typeof(SplashProjectile).GetField("falloffExponent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    radiusField?.SetValue(splashProj, CurrentSplashRadius);
                    falloffField?.SetValue(splashProj, _data.SplashFalloffExponent);
                }
                if (_data.IsSlow && projectileObj.TryGetComponent(out SlowProjectile slowProj))
                {
                    var slowFactorField = typeof(SlowProjectile).GetField("slowFactor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var slowDurationField = typeof(SlowProjectile).GetField("slowDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    slowFactorField?.SetValue(slowProj, CurrentSlowFactor);
                    slowDurationField?.SetValue(slowProj, CurrentSlowDuration);
                }
                BulletHeavenFortressDefense.Projectiles.SniperProjectile sniperProjComponent = null;
                bool sniperFlag = (_data.IsSniper || _treatAsSniper);
                if (sniperFlag)
                {
                    projectileObj.TryGetComponent(out sniperProjComponent); // ignore result; null if absent
                }

                // IMPORTANT: First call generic Initialize so SniperProjectile captures TowerData (speed override etc.)
                if (projectileObj.TryGetComponent(out ITowerProjectile projectile))
                {
                    projectile.Initialize(_data, direction.normalized, _data.ProjectilePoolId);
                    _lastUsedProjectileSpeed = projectile is Projectile p ? p.Speed : _data.ProjectileSpeedBase;
                }
                else if (projectileObj.TryGetComponent(out Projectile legacyProjectile))
                {
                    legacyProjectile.Initialize(_data, direction.normalized, _data.ProjectilePoolId);
                    _lastUsedProjectileSpeed = legacyProjectile.Speed;
                }

                // Apply runtime overrides AFTER base init.
                if (sniperFlag && sniperProjComponent != null)
                {
                    float finalDmg = CurrentDamage;
                    int pierce = _data.SniperPierceCount; // negative means infinite
                    sniperProjComponent.SetRuntimeOverrides(finalDmg, pierce, _data.SniperCritChance, _data.SniperCritMultiplier);
                }
            }
        }

        private Vector3 ComputeAimDirection(EnemyController target, Vector3 origin)
        {
            if (target == null) return (target?.Position ?? origin) - origin;
            Vector3 targetPos = target.Position;
            Vector3 targetVel = target.Velocity; // requires EnemyController velocity property
            float projSpeed = _data != null && _data.ProjectileSpeedBase > 0f ? _data.ProjectileSpeedBase : (_lastUsedProjectileSpeed > 0f ? _lastUsedProjectileSpeed : 8f);
            if (projSpeed <= 0.01f)
            {
                return targetPos - origin; // cannot compute lead
            }

            Vector3 relPos = targetPos - origin;
            Vector3 relVel = targetVel; // shooter assumed stationary
            float a = relVel.sqrMagnitude - projSpeed * projSpeed;
            float b = 2f * Vector3.Dot(relPos, relVel);
            float c = relPos.sqrMagnitude;

            float t;
            if (Mathf.Abs(a) < 0.0001f)
            {
                // Treat as linear (target velocity magnitude equals projectile speed roughly or target nearly stationary)
                float projMag = projSpeed;
                float dist = relPos.magnitude;
                t = projMag > 0f ? dist / projMag : 0f;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                {
                    return relPos; // no real solution
                }
                float sqrt = Mathf.Sqrt(disc);
                float t1 = (-b + sqrt) / (2f * a);
                float t2 = (-b - sqrt) / (2f * a);
                t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : (t1 > 0f ? t1 : t2);
                if (t < 0f || float.IsNaN(t) || float.IsInfinity(t)) return relPos;
            }

            t *= Mathf.Clamp(leadBias, 0.05f, 4f);
            t = Mathf.Clamp(t, 0f, Mathf.Max(0.01f, maxLeadTime));
            Vector3 aimPoint = targetPos + targetVel * t;
            return aimPoint - origin;
        }
    }
}
