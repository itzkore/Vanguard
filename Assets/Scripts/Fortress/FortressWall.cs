using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Fortress
{
    public class FortressWall : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 50;
        [SerializeField] private int repairCost = 20;
        [SerializeField] private FortressMount mount; // legacy single reference for prefab compatibility
        [SerializeField] private FortressMount[] mounts; // supports multiple mounts per wall
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private UI.WallDamageOverlay damageOverlay;
    [Header("Segmented Health (5 parts)")]
    [SerializeField] private bool segmented = false;
    [Tooltip("Segment health order: 0=UL, 1=UR, 2=LL, 3=LR, 4=Center")]
    [SerializeField] private int[] segmentHealth = new int[5];
    [SerializeField, Range(0.05f, 0.45f)] private float centerRegionHalfSize = 0.2f; // local-space half-size for center segment

        private int _currentHealth;
        private FortressManager _manager;
        private int _row;
        private int _column;
        private bool _destroyed;
    private Collider2D[] _ownColliders;

    public FortressMount Mount => (mounts != null && mounts.Length > 0) ? mounts[0] : mount;
    public System.Collections.Generic.IReadOnlyList<FortressMount> Mounts => mounts;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => _currentHealth;
    public int RepairCost => repairCost;
        public bool IsDestroyed => _destroyed;
    public int Row => _row;
    public int Column => _column;

        public void Initialize(FortressManager manager, int row, int column)
        {
            _manager = manager;
            _row = row;
            _column = column;
            _currentHealth = Mathf.Max(1, maxHealth);
            // Non-segmented model: _currentHealth handles wall health entirely
            _destroyed = false;

            if (sprite == null)
            {
                sprite = GetComponentInChildren<SpriteRenderer>();
            }

            // Ensure damage overlay exists
            if (damageOverlay == null)
            {
                damageOverlay = GetComponent<UI.WallDamageOverlay>();
                if (damageOverlay == null)
                {
                    damageOverlay = gameObject.AddComponent<UI.WallDamageOverlay>();
                }
            }
            UpdateDamageOverlay();

            // Cache own colliders (root-level) for enabling/disabling on destroy/repair
            _ownColliders = GetComponents<Collider2D>();

            // Auto-find mounts (supports multiple children)
            if (mounts == null || mounts.Length == 0)
            {
                mounts = GetComponentsInChildren<FortressMount>(includeInactive: true);
                if ((mounts == null || mounts.Length == 0) && mount != null)
                {
                    mounts = new[] { mount };
                }
            }

            if (mounts != null)
            {
                for (int i = 0; i < mounts.Length; i++)
                {
                    var m = mounts[i];
                    if (m != null)
                    {
                        m.Initialize(this, row, column);
                    }
                }
            }
        }

        public void TakeDamage(float amount, DamageType damageType)
        {
            if (_destroyed)
            {
                return;
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(amount));
            _currentHealth -= damage;

            UpdateDamageOverlay();

            if (_currentHealth <= 0)
            {
                HandleDestroyed();
            }
        }

        public void TakeDamageAtPoint(float amount, DamageType damageType, Vector2 worldPoint)
        {
            if (_destroyed)
            {
                return;
            }
            int damage2 = Mathf.Max(1, Mathf.RoundToInt(amount));
            _currentHealth -= damage2;

            UpdateDamageOverlay();

            if (_currentHealth <= 0)
            {
                HandleDestroyed();
            }
        }

        private void ApplyDamageToSegmentOrSpill(int idx, int damage, Vector2 localHit)
        {
            // If the intended segment is still alive, apply directly
            if (idx >= 0 && idx < 5 && segmentHealth[idx] > 0)
            {
                ApplyDamageToSegment(idx, damage);
                return;
            }

            // Otherwise spill to the nearest alive segment (by distance to rough segment centers)
            float a = Mathf.Max(0.05f, centerRegionHalfSize);
            Vector2[] centers = new Vector2[5]
            {
                new Vector2(-a, +a), // UL
                new Vector2(+a, +a), // UR
                new Vector2(-a, -a), // LL
                new Vector2(+a, -a), // LR
                Vector2.zero          // Center
            };

            int bestIdx = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                if (segmentHealth[i] <= 0) continue;
                float d = (centers[i] - localHit).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                ApplyDamageToSegment(bestIdx, damage);
            }
            // else: all segments dead; nothing to do
        }

        private void ApplyDamageToSegment(int idx, int damage)
        {
            if (idx < 0 || idx > 4) return;
            if (segmentHealth[idx] <= 0) return;
            segmentHealth[idx] -= damage;
            if (segmentHealth[idx] < 0) segmentHealth[idx] = 0;
        }

        private void HandleDestroyed()
        {
            _destroyed = true;
            _currentHealth = 0;

            if (sprite != null)
            {
                // Dark gray final state
                sprite.color = new Color(0.22f, 0.22f, 0.22f, 1f);
            }

            UpdateDamageOverlay();
            // Remove red damage tint at final state
            if (damageOverlay != null)
            {
                damageOverlay.SetDestroyedAppearance();
            }

            // Disable barrier colliders so enemies can reach the core
            if (_ownColliders != null)
            {
                for (int i = 0; i < _ownColliders.Length; i++)
                {
                    var c = _ownColliders[i];
                    if (c == null) continue;
                    if (!c.isTrigger)
                    {
                        c.enabled = false;
                    }
                }
            }

            if (mounts != null)
            {
                for (int i = 0; i < mounts.Length; i++)
                {
                    var m = mounts[i];
                    if (m != null)
                    {
                        m.DestroyMountedTower();
                        m.SetAvailable(false);
                    }
                }
            }

            _manager?.NotifyWallDestroyed(this);

            // Notify player that towers on this block were destroyed
            BulletHeavenFortressDefense.UI.HUDController.Toast("Wall destroyed: mounted towers lost. Rebuild if needed.");
        }

        public bool TryRepair() => TryRepairAny();

        /// <summary>
        /// Returns the energy cost to fully repair from the current state to max health.
        /// Destroyed: full base repairCost. Damaged: proportional to missing HP (ceil).
        /// </summary>
        public int GetRepairCostForMissing()
        {
            if (_currentHealth >= maxHealth && !_destroyed) return 0;
            if (_destroyed) return repairCost;
            int missing = Mathf.Max(0, maxHealth - _currentHealth);
            if (missing == 0) return 0;
            float perHp = repairCost / Mathf.Max(1f, (float)maxHealth);
            int cost = Mathf.CeilToInt(perHp * missing);
            return Mathf.Max(1, cost);
        }

        /// <summary>
        /// Repairs destroyed or partially damaged wall. Charges economy appropriately.
        /// </summary>
        public bool TryRepairAny()
        {
            int cost = GetRepairCostForMissing();
            if (cost <= 0) return false; // already full
            // Phase gate: only allow during non-combat build phases
            if (BulletHeavenFortressDefense.Managers.WaveManager.HasInstance)
            {
                var phase = BulletHeavenFortressDefense.Managers.WaveManager.Instance.CurrentPhase;
                bool buildPhase = phase == BulletHeavenFortressDefense.Managers.WaveManager.WavePhase.Shop
                                   || phase == BulletHeavenFortressDefense.Managers.WaveManager.WavePhase.Preparation;
                if (!buildPhase)
                {
                    BulletHeavenFortressDefense.UI.HUDController.Toast("Repairs disabled during combat");
                    return false;
                }
            }
            if (!EconomySystem.HasInstance || !EconomySystem.Instance.TrySpend(cost)) return false;
            ForceFullRepair();
            return true;
        }

        /// <summary>
        /// Restores wall to full health, re-enables colliders & mounts, and notifies manager. Does NOT spend energy.
        /// </summary>
        public void ForceFullRepair()
        {
            _currentHealth = maxHealth;
            if (segmented)
            {
                int per = Mathf.Max(1, maxHealth / 5);
                int remainder = Mathf.Max(0, maxHealth - per * 5);
                for (int i = 0; i < 5; i++)
                {
                    segmentHealth[i] = per + (i < remainder ? 1 : 0);
                }
            }
            _destroyed = false;

            if (sprite != null) sprite.color = Color.white;
            UpdateDamageOverlay();

            // Re-enable colliders
            if (_ownColliders != null)
            {
                for (int i = 0; i < _ownColliders.Length; i++)
                {
                    var c = _ownColliders[i];
                    if (c == null) continue;
                    if (!c.isTrigger) c.enabled = true;
                }
            }

            if (mounts != null)
            {
                for (int i = 0; i < mounts.Length; i++)
                {
                    var m = mounts[i];
                    if (m != null) m.SetAvailable(true);
                }
            }
            _manager?.NotifyWallRepaired(this);
        }

        public bool IsAlive => !_destroyed;

        private void UpdateDamageOverlay()
        {
            if (damageOverlay != null)
            {
                float pct = Mathf.Clamp01(_currentHealth / Mathf.Max(1f, (float)maxHealth));
                damageOverlay.SetPercent(pct);
            }
        }
    }
}