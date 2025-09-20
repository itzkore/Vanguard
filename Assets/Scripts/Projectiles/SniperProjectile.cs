using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Managers; // ObjectPoolManager
using BulletHeavenFortressDefense.FX; // DamageTextManager

namespace BulletHeavenFortressDefense.Projectiles
{
    /// <summary>
    /// High-speed piercing projectile (restored). Uses sweep to avoid tunneling and supports crit + pierce.
    /// NOTE: Viewport culling removed to prevent premature despawn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SniperProjectile : MonoBehaviour, ITowerProjectile
    {
        [SerializeField, Tooltip("Base movement speed (units/sec). Can be overridden by TowerData.SniperProjectileSpeed.")] private float speed = 18f;
        [SerializeField, Tooltip("Seconds before auto-despawn if it never hits.")] private float lifetime = 4f;
        [SerializeField, Range(0.05f,0.5f), Tooltip("Radius used for sweep tests.")] private float hitRadius = 0.1f;
        [Header("Detection")]
        [SerializeField, Tooltip("If true performs a CircleCast sweep each frame for high speed collision.")] private bool useContinuousSweep = true;
        [SerializeField, Tooltip("Show floating damage numbers on hit.")] private bool showDamageText = true;
        [SerializeField, Tooltip("Debug remaining pierce logging.")] private bool debugPierce = false;
        [Header("Trail FX")] [SerializeField, Tooltip("Secondary trail width multiplier.")] private float trailGlowWidthMult = 1.8f;
        [SerializeField] private bool debugLine = false;

        private TowerData _data;
        private Vector3 _direction = Vector3.right;
        private string _poolId;
        private float _lifeTimer;
        private int _remainingHits;
        private readonly System.Collections.Generic.HashSet<int> _hitEnemyInstanceIds = new();
        private bool _initialized;
        private float _damage;
        private float _critChance;
        private float _critMult;

        public void Initialize(TowerData data, Vector3 direction, string poolId)
        {
            _data = data;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            _poolId = poolId;
            _lifeTimer = lifetime;
            _initialized = true;
            transform.right = _direction;

            if (data != null)
            {
                if (data.SniperProjectileSpeed > 0f)
                {
                    speed = data.SniperProjectileSpeed;
                }
                else
                {
                    float baseline = data.ProjectileSpeedBase > 0f ? data.ProjectileSpeedBase : speed;
                    speed = baseline * 8f; // distinct fast feel
                }
            }

            _damage = data != null ? data.Damage : 0f;
            _critChance = data != null ? data.SniperCritChance : 0f;
            _critMult = data != null ? data.SniperCritMultiplier : 2f;
            int total = data != null ? data.SniperPierceCount : 1;
            _remainingHits = total < 0 ? int.MaxValue : Mathf.Max(1, total);
            _hitEnemyInstanceIds.Clear();
            EnsureTrail();
        }

        public void SetRuntimeOverrides(float damage, int totalHits, float critChance, float critMult)
        {
            _damage = Mathf.Max(0f, damage);
            _remainingHits = totalHits < 0 ? int.MaxValue : Mathf.Max(1, totalHits);
            _critChance = Mathf.Clamp01(critChance);
            _critMult = Mathf.Max(1f, critMult);
        }

        private void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            _lifeTimer -= dt;
            if (_lifeTimer <= 0f) { Despawn(); return; }

            Vector3 startPos = transform.position;
            Vector3 move = _direction * speed * dt;
            Vector3 endPos = startPos + move;

            if (useContinuousSweep)
            {
                float distance = move.magnitude;
                if (distance > 0f)
                {
                    var hits = Physics2D.CircleCastAll(startPos, hitRadius, _direction, distance);
                    if (hits != null && hits.Length > 0)
                    {
                        System.Array.Sort(hits, (a,b)=> a.distance.CompareTo(b.distance));
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var ec = hits[i].collider != null ? hits[i].collider.GetComponent<EnemyController>() : null;
                            if (ec == null || !ec.IsAlive) continue;
                            if (!ProcessHit(ec)) return; // despawned
                        }
                    }
                }
                transform.position = endPos;
            }
            else
            {
                transform.position = endPos;
                var overlaps = Physics2D.OverlapCircleAll(endPos, hitRadius);
                for (int i = 0; overlaps != null && i < overlaps.Length; i++)
                {
                    var ec = overlaps[i].GetComponent<EnemyController>();
                    if (ec == null || !ec.IsAlive) continue;
                    if (!ProcessHit(ec)) return;
                }
            }
        }

        private bool ProcessHit(EnemyController ec)
        {
            int id = ec.GetInstanceID();
            if (_hitEnemyInstanceIds.Contains(id)) return true;
            if (_remainingHits == 0) { Despawn(); return false; }

            _hitEnemyInstanceIds.Add(id);
            var (dmg, crit) = ComputeDamage();
            var dmgType = _data != null ? _data.DamageType : DamageType.Physical;
            ec.TakeDamage(dmg, dmgType);
            if (showDamageText && DamageTextManager.HasInstance)
            {
                DamageTextManager.Show(ec.transform.position, dmg, crit);
            }
            if (_remainingHits != int.MaxValue) _remainingHits--;
            if (debugPierce) Debug.Log($"[SniperProjectile] Hit {ec.name} remaining={(_remainingHits==int.MaxValue?"INF":_remainingHits.ToString())}");
            if (_remainingHits == 0) { Despawn(); return false; }
            return true;
        }

        private (float dmg, bool crit) ComputeDamage()
        {
            float d = _damage;
            bool c = false;
            if (_critChance > 0f && Random.value <= _critChance)
            {
                d *= _critMult; c = true;
            }
            return (d,c);
        }

        private void Despawn()
        {
            if (!string.IsNullOrEmpty(_poolId) && ObjectPoolManager.HasInstance)
                ObjectPoolManager.Instance.Release(_poolId, gameObject);
            else
                Destroy(gameObject);
        }

        private void EnsureTrail()
        {
            var primary = GetComponent<TrailRenderer>();
            if (primary == null)
            {
                primary = gameObject.AddComponent<TrailRenderer>();
                primary.time = 0.42f; primary.startWidth = 0.07f; primary.endWidth = 0f;
                primary.numCapVertices = 4; primary.numCornerVertices = 2;
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]{
                        new GradientColorKey(new Color(1f,1f,1f,1f),0f),
                        new GradientColorKey(new Color(0.55f,0.85f,1f,1f),0.35f),
                        new GradientColorKey(new Color(0.15f,0.35f,0.75f,0.6f),1f)
                    },
                    new GradientAlphaKey[]{
                        new GradientAlphaKey(1f,0f),
                        new GradientAlphaKey(0.9f,0.25f),
                        new GradientAlphaKey(0f,1f)
                    });
                primary.colorGradient = grad;
                primary.material = new Material(Shader.Find("Sprites/Default"));
            }
            if (transform.Find("GlowTrail") == null)
            {
                var glowGO = new GameObject("GlowTrail");
                glowGO.transform.SetParent(transform, false);
                var glow = glowGO.AddComponent<TrailRenderer>();
                glow.time = primary.time * 0.9f;
                glow.startWidth = primary.startWidth * trailGlowWidthMult;
                glow.endWidth = 0f;
                glow.numCapVertices = 4; glow.numCornerVertices = 2;
                var ggrad = new Gradient();
                ggrad.SetKeys(
                    new GradientColorKey[]{
                        new GradientColorKey(new Color(0.4f,0.85f,1f,0.6f),0f),
                        new GradientColorKey(new Color(0.1f,0.35f,0.8f,0.2f),1f)
                    },
                    new GradientAlphaKey[]{
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
