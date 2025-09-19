using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "BHFD/Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private float health = 20f;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private int reward = 5;
        [SerializeField] private DamageResistance[] resistances;
        [SerializeField] private string poolId;

        [Header("Ranged Combat")]
        [SerializeField] private bool canShoot = true;
        [SerializeField] private float rangedDamage = 3f;
        [SerializeField] private float rangedFireRate = 1.2f;
        [SerializeField] private float rangedRange = 2f;
        [SerializeField] private DamageType rangedDamageType = DamageType.Physical;
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private string projectilePoolId;

    [Header("Blood FX (Optional)")]
    [SerializeField, Tooltip("Prefab for small hit blood effect (particles or sprite). Optional.")] private GameObject hitBloodPrefab;
    [SerializeField, Tooltip("Prefab for death blood effect (bigger burst). Optional.")] private GameObject deathBloodPrefab;
    [SerializeField, Tooltip("If true, auto-rotate hit effect in direction of incoming damage (if direction known). ")] private bool orientBloodToHit = true;
    [SerializeField, Tooltip("Minimum seconds between spawning hit blood on the SAME enemy (throttle). ")] private float hitBloodCooldown = 0.08f;


        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public float Health => health;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public int Reward => reward;
        public DamageResistance[] Resistances => resistances;
        public string PoolId => poolId;

    public bool CanShoot => canShoot;
    public float RangedDamage => rangedDamage;
    public float RangedFireRate => rangedFireRate;
    public float RangedRange => rangedRange;
    public DamageType RangedDamageType => rangedDamageType;
    public float ProjectileSpeed => projectileSpeed;
    public GameObject ProjectilePrefab => projectilePrefab;
    public string ProjectilePoolId => projectilePoolId;
    public GameObject HitBloodPrefab => hitBloodPrefab;
    public GameObject DeathBloodPrefab => deathBloodPrefab;
    public bool OrientBloodToHit => orientBloodToHit;
    public float HitBloodCooldown => Mathf.Max(0f, hitBloodCooldown);

        public float GetResistanceModifier(DamageType damageType)
        {
            if (resistances == null)
            {
                return 1f;
            }

            foreach (var resistance in resistances)
            {
                if (resistance.damageType == damageType)
                {
                    return resistance.multiplier;
                }
            }

            return 1f;
        }
    }

    [System.Serializable]
    public struct DamageResistance
    {
        public DamageType damageType;
        public float multiplier;
    }
}
