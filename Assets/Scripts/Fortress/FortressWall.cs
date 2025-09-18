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
    [SerializeField] private bool segmented = true;
    [Tooltip("Segment health order: 0=UL, 1=UR, 2=LL, 3=LR, 4=Center")]
    [SerializeField] private int[] segmentHealth = new int[5];
    [SerializeField, Range(0.05f, 0.45f)] private float centerRegionHalfSize = 0.2f; // local-space half-size for center segment

        private int _currentHealth;
        private FortressManager _manager;
        private int _row;
        private int _column;
        private bool _destroyed;

    public FortressMount Mount => (mounts != null && mounts.Length > 0) ? mounts[0] : mount;
    public System.Collections.Generic.IReadOnlyList<FortressMount> Mounts => mounts;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => segmented ? (segmentHealth[0] + segmentHealth[1] + segmentHealth[2] + segmentHealth[3] + segmentHealth[4]) : _currentHealth;
        public bool IsDestroyed => _destroyed;
    public int Row => _row;
    public int Column => _column;

        public void Initialize(FortressManager manager, int row, int column)
        {
            _manager = manager;
            _row = row;
            _column = column;
            _currentHealth = Mathf.Max(1, maxHealth);
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
            if (segmented)
            {
                // Distribute to the healthiest remaining segment (fallback when no hit point provided)
                int target = 0;
                int best = int.MinValue;
                for (int i = 0; i < 5; i++)
                {
                    if (segmentHealth[i] > best)
                    {
                        best = segmentHealth[i];
                        target = i;
                    }
                }
                ApplyDamageToSegment(target, damage);
            }
            else
            {
                _currentHealth -= damage;
            }

            UpdateDamageOverlay();

            if ((segmented && (segmentHealth[0] + segmentHealth[1] + segmentHealth[2] + segmentHealth[3] + segmentHealth[4]) <= 0) || (!segmented && _currentHealth <= 0))
            {
                HandleDestroyed();
            }
        }

        public void TakeDamageAtPoint(float amount, DamageType damageType, Vector2 worldPoint)
        {
            if (!segmented)
            {
                TakeDamage(amount, damageType);
                return;
            }

            if (_destroyed)
            {
                return;
            }

            // Determine segment relative to wall's local space: center box then quadrants
            Vector2 local = transform.InverseTransformPoint(worldPoint);
            int idx;
            if (Mathf.Abs(local.x) <= centerRegionHalfSize && Mathf.Abs(local.y) <= centerRegionHalfSize)
            {
                idx = 4; // Center
            }
            else if (local.y >= 0f)
            {
                idx = (local.x < 0f) ? 0 : 1; // UL : UR
            }
            else
            {
                idx = (local.x < 0f) ? 2 : 3; // LL : LR
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(amount));
            ApplyDamageToSegment(idx, damage);

            UpdateDamageOverlay();

            if ((segmentHealth[0] + segmentHealth[1] + segmentHealth[2] + segmentHealth[3] + segmentHealth[4]) <= 0)
            {
                HandleDestroyed();
            }
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
                sprite.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            UpdateDamageOverlay();

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
        }

        public bool TryRepair()
        {
            if (!_destroyed)
            {
                return false;
            }

            if (!EconomySystem.HasInstance || !EconomySystem.Instance.TrySpend(repairCost))
            {
                return false;
            }

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

            if (sprite != null)
            {
                sprite.color = Color.white;
            }

            UpdateDamageOverlay();

            if (mounts != null)
            {
                for (int i = 0; i < mounts.Length; i++)
                {
                    var m = mounts[i];
                    if (m != null)
                    {
                        m.SetAvailable(true);
                    }
                }
            }

            return true;
        }

        public bool IsAlive => !_destroyed;

        private void UpdateDamageOverlay()
        {
            if (damageOverlay != null)
            {
                if (segmented)
                {
                    float denom = Mathf.Max(1f, (float)maxHealth / 5f);
                    float p0 = Mathf.Clamp01(segmentHealth[0] / denom);
                    float p1 = Mathf.Clamp01(segmentHealth[1] / denom);
                    float p2 = Mathf.Clamp01(segmentHealth[2] / denom);
                    float p3 = Mathf.Clamp01(segmentHealth[3] / denom);
                    float p4 = Mathf.Clamp01(segmentHealth[4] / denom);
                    damageOverlay.SetPercents(p0, p1, p2, p3, p4);
                }
                else
                {
                    float pct = Mathf.Clamp01(_currentHealth / Mathf.Max(1f, (float)maxHealth));
                    damageOverlay.SetPercent(pct);
                }
            }
        }
    }
}