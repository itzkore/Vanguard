using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Managers; // for ObjectPoolManager
using BulletHeavenFortressDefense.FX; // for DamageTextManager

namespace BulletHeavenFortressDefense.Projectiles
{
    // High-damage, piercing projectile with optional crit handling.
    public class SniperProjectile : MonoBehaviour, ITowerProjectile
    {
        [SerializeField, Tooltip("Base movement speed (units/sec). Can be overridden by TowerData.SniperProjectileSpeed.")] private float speed = 18f;
        [SerializeField, Tooltip("Seconds before auto-destroy if it never hits anything.")] private float lifetime = 4f;
        [SerializeField, Range(0.05f,0.5f), Tooltip("Radius used for circle sweep / overlap tests.")] private float hitRadius = 0.1f;
    [Header("Advanced Detection")]
        [SerializeField, Tooltip("If true uses a continuous CircleCast sweep each frame to prevent tunnelling at very high speeds.")] private bool useContinuousSweep = true;
        [SerializeField, Tooltip("Show floating damage numbers on hit.")] private bool showDamageText = true;
    [SerializeField, Tooltip("Verbose logging of hit + remaining pierce (for debugging)")] private bool debugPierce = false;

        private TowerData _data;
        private Vector3 _direction;
        private string _poolId;
        private float _lifeTimer;
        // Remaining TOTAL hits (each enemy counts as 1). -1 from data => infinite (int.MaxValue).
        private int _remainingHits;
    private readonly System.Collections.Generic.HashSet<int> _hitEnemyInstanceIds = new();
    [SerializeField, Tooltip("Extra trail glow width multiplier")] private float trailGlowWidthMult = 1.8f;
    [SerializeField, Tooltip("If true draws a gizmo line for direction (debug)")] private bool debugLine = false;
        private bool _initialized;
        private float _damage;
        private float _critChance;
        private float _critMult;

        public void Initialize(TowerData data, Vector3 direction, string poolId)
        {
            _data = data;
            _direction = direction.normalized;
            _poolId = poolId;
            _lifeTimer = lifetime;
            _initialized = true;
            transform.right = _direction; // orient sprite/mesh

            // If TowerData supplies an override speed, apply it
            if (data != null && data.SniperProjectileSpeed > 0f)
            {
                speed = data.SniperProjectileSpeed;
            }

            // Compute final damage snapshot (sniper multipliers already applied in TowerBehaviour when calling Launch)
            _damage = data != null ? data.Damage : 0f; // Will be overridden by TowerBehaviour injection if desired.
            _critChance = data != null ? data.SniperCritChance : 0f;
            _critMult = data != null ? data.SniperCritMultiplier : 2f;
            int total = data != null ? data.SniperPierceCount : 1;
            if (total < 0)
                _remainingHits = int.MaxValue; // infinite
            else
                _remainingHits = Mathf.Max(1, total); // ensure at least 1 target
            _hitEnemyInstanceIds.Clear();
            EnsureTrail();
        }

        // Allow TowerBehaviour to inject final computed damage (after all scaling) and possibly override pierce.
        public void SetRuntimeOverrides(float damage, int totalHits, float critChance, float critMult)
        {
            _damage = Mathf.Max(0f, damage);
            if (totalHits < 0)
                _remainingHits = int.MaxValue;
            else
                _remainingHits = Mathf.Max(1, totalHits);
            _critChance = Mathf.Clamp01(critChance);
            _critMult = Mathf.Max(1f, critMult);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            float dt = Time.deltaTime;
            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Despawn();
                return;
            }

            Vector3 startPos = transform.position;
            Vector3 move = _direction * speed * dt;
            Vector3 endPos = startPos + move;

            if (useContinuousSweep)
            {
                float distance = move.magnitude;
                if (distance > 0f)
                {
                    var rayHits = Physics2D.CircleCastAll(startPos, hitRadius, _direction, distance);
                    if (rayHits != null && rayHits.Length > 0)
                    {
                        System.Array.Sort(rayHits, (a, b) => a.distance.CompareTo(b.distance));
                        for (int i = 0; i < rayHits.Length; i++)
                        {
                            var ec = rayHits[i].collider != null ? rayHits[i].collider.GetComponent<EnemyController>() : null;
                            if (ec == null || !ec.IsAlive) continue;
                            if (!ProcessPotentialHit(ec)) return; // projectile despawned
                        }
                    }
                }
                transform.position = endPos;
            }
            else
            {
                transform.position = endPos;
                var overlaps = Physics2D.OverlapCircleAll(transform.position, hitRadius);
                if (overlaps != null && overlaps.Length > 0)
                {
                    for (int i = 0; i < overlaps.Length; i++)
                    {
                        var ec = overlaps[i].GetComponent<EnemyController>();
                        if (ec == null || !ec.IsAlive) continue;
                        if (!ProcessPotentialHit(ec)) return; // projectile despawned
                    }
                }
            }
        }

        private bool ProcessPotentialHit(EnemyController ec)
        {
            // Already processed this enemy? Skip.
            int id = ec.GetInstanceID();
            if (_hitEnemyInstanceIds.Contains(id)) return true;
            if (_remainingHits == 0)
            {
                Despawn();
                return false; // no hits left
            }

            // Record + apply damage
            _hitEnemyInstanceIds.Add(id);
            var (dmg, crit) = ComputeDamage();
            DamageType dmgType = _data != null ? _data.DamageType : DamageType.Physical;
            ec.TakeDamage(dmg, dmgType);
            if (showDamageText && DamageTextManager.HasInstance)
            {
                DamageTextManager.Show(ec.transform.position, dmg, crit);
            }
            if (_remainingHits != int.MaxValue)
            {
                _remainingHits--;
            }
            if (debugPierce)
            {
                Debug.Log($"[SniperProjectile] Hit {ec.name} remaining={(_remainingHits==int.MaxValue?"INF":_remainingHits.ToString())}");
            }
            if (_remainingHits == 0)
            {
                Despawn();
                return false;
            }
            return true;
        }

        private (float damage, bool crit) ComputeDamage()
        {
            float dmg = _damage;
            bool crit = false;
            if (_critChance > 0f && Random.value <= _critChance)
            {
                dmg *= _critMult;
                crit = true;
            }
            return (dmg, crit);
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

        private void EnsureTrail()
        {
            // Primary trail (thin core)
            TrailRenderer primary = GetComponent<TrailRenderer>();
            if (primary == null)
            {
                primary = gameObject.AddComponent<TrailRenderer>();
                primary.time = 0.42f;
                primary.startWidth = 0.07f;
                primary.endWidth = 0.0f;
                primary.numCapVertices = 4;
                primary.numCornerVertices = 2;
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(1f,1f,1f,1f), 0f),
                        new GradientColorKey(new Color(0.55f,0.85f,1f,1f), 0.35f),
                        new GradientColorKey(new Color(0.15f,0.35f,0.75f,0.6f), 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1f,0f),
                        new GradientAlphaKey(0.9f,0.25f),
                        new GradientAlphaKey(0f,1f)
                    });
                primary.colorGradient = grad;
                primary.material = new Material(Shader.Find("Sprites/Default"));
            }

            // Secondary glow trail (wider, faint)
            if (transform.Find("GlowTrail") == null)
            {
                var glowGO = new GameObject("GlowTrail");
                glowGO.transform.SetParent(transform, false);
                var glow = glowGO.AddComponent<TrailRenderer>();
                glow.time = primary.time * 0.9f;
                glow.startWidth = primary.startWidth * trailGlowWidthMult;
                glow.endWidth = 0f;
                glow.numCapVertices = 4;
                glow.numCornerVertices = 2;
                var ggrad = new Gradient();
                ggrad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0.4f,0.85f,1f,0.6f), 0f),
                        new GradientColorKey(new Color(0.1f,0.35f,0.8f,0.2f), 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.55f,0f),
                        new GradientAlphaKey(0f,1f)
                    });
                glow.colorGradient = ggrad;
                glow.material = new Material(Shader.Find("Sprites/Default"));
                glow.sortingOrder = primary.sortingOrder - 1;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugLine) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + _direction * 0.8f);
        }
    }
}
