using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "BHFD/Data/Tower")]
    public class TowerData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private string projectilePoolId;
        [SerializeField, Tooltip("Base projectile speed (units/sec) for standard Projectile & as baseline for others. 0 or negative = use prefab default.")] private float projectileSpeedBase = 0f;
        [SerializeField] private int buildCost = 10;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private float range = 3f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private TargetPriority targetPriority = TargetPriority.ClosestToTower;
        [SerializeField] private bool rotateTowardsTarget = true;

    [Header("Progression")]
    [SerializeField, Tooltip("Max upgrade level including the starting level.")] private int maxLevel = 10; // increased from 3
    [SerializeField, Tooltip("Base cost of the first upgrade (from level 1 to 2).")] private int upgradeCostBase = 20;
    [SerializeField, Tooltip("Each next upgrade cost = previous * growth (rounded). ")] private float upgradeCostGrowth = 1.5f;
    [SerializeField, Tooltip("Additional per-tower multiplier applied AFTER global upgrade multiplier (use this to make certain towers more expensive). 1 = no change.")] private float upgradeCostExtraMult = 1f;
    [SerializeField, Tooltip("Refund percent when selling (of total invested).")] [Range(0f, 1f)] private float sellRefundPercent = 0.6f;
    [SerializeField, Tooltip("Fire rate multiplier applied per level after level 1 (exponential). 1.15 means +15% fire rate per level")] private float fireRatePerLevelMult = 1.15f;
    [SerializeField, Tooltip("Range multiplier applied per level after level 1 (exponential). 1.1 means +10% range per level")] private float rangePerLevelMult = 1.1f;
    [SerializeField, Tooltip("Damage multiplier applied per level after level 1 (exponential). 1.0 = no damage scaling.")] private float damagePerLevelMult = 1.0f;
    [SerializeField, Tooltip("Flat damage added per level after level 1 (additive). 0 = none.")] private float damageFlatPerLevel = 0f;

    [Header("Specialized: Splash (AoE)")]
    [SerializeField, Tooltip("If true this tower's projectile creates an AoE explosion (SplashProjectile). Radius scales by fields below.")] private bool isSplash = false;
    [SerializeField, Tooltip("Base explosion radius at level 1.")] private float splashRadiusBase = 1.5f;
    [SerializeField, Tooltip("Multiplicative radius growth per level above 1. 1 = no multiplicative growth.")] private float splashRadiusPerLevelMult = 1.15f;
    [SerializeField, Tooltip("Flat radius added per level above 1 (after multiplicative). 0 = none.")] private float splashRadiusPerLevelFlat = 0f;
    [SerializeField, Tooltip("Falloff exponent (1 = linear, 2 = quadratic) for damage inside the explosion.")] private float splashFalloffExponent = 1f;

    [Header("Specialized: Slow Tower")]
    [SerializeField, Tooltip("If true this tower applies slow via SlowProjectile and can fire multiple projectiles per shot.")] private bool isSlow = false;
    [SerializeField, Tooltip("Base slow factor (final speed multiplier) at level 1. 0.5 = 50% speed.")] private float slowFactorBase = 0.5f;
    [SerializeField, Tooltip("Additive change to slow factor per level above 1 (negative makes enemies slower). Example -0.03 makes each level 3% slower. Clamped to >= 0.05.")] private float slowFactorPerLevelAdd = -0.03f;
    [SerializeField, Tooltip("Base slow duration (seconds) at level 1.")] private float slowDurationBase = 2f;
    [SerializeField, Tooltip("Additive duration seconds per level above 1.")] private float slowDurationPerLevelAdd = 0.25f;
    [SerializeField, Tooltip("Base number of projectiles fired per shot at level 1.")] private int projectilesPerShotBase = 1;
    [SerializeField, Tooltip("Additional (fractional) projectiles per level above 1. Final count = base + floor(levelsAbove * perLevel)." )] private float projectilesPerShotPerLevel = 0.25f;
    [SerializeField, Tooltip("Global damage multiplier applied to this tower's projectiles AFTER normal damage scaling (lets slow towers have reduced damage). 1 = unchanged.")] private float slowTowerDamageMultiplier = 0.6f;

    [Header("Specialized: Sniper Tower")] 
    [SerializeField, Tooltip("If true this tower behaves as a sniper: very long range, high damage, optional pierce / crit.")] private bool isSniper = false;
    [SerializeField, Tooltip("Additional multiplicative damage bonus applied AFTER normal + level scaling (sniper identity). 1 = none.")] private float sniperDamageMultiplier = 3.0f;
    [SerializeField, Tooltip("TOTAL number of targets a sniper projectile can hit. 1 = only first (no pierce). -1 = infinite.")] private int sniperPierceCount = 2;
    [SerializeField, Tooltip("Critical hit chance (0-1) per shot.")] [Range(0f,1f)] private float sniperCritChance = 0.15f;
    [SerializeField, Tooltip("Critical damage multiplier.")] private float sniperCritMultiplier = 2.5f;
    [SerializeField, Tooltip("Flat range multiplier applied if sniper (lets us push range massively without affecting others). 1 = none.")] private float sniperRangeMultiplier = 2.5f;
    [SerializeField, Tooltip("Override projectile speed for sniper (units/sec). <= 0 means use projectile prefab default.")] private float sniperProjectileSpeed = 0f;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public string ProjectilePoolId => projectilePoolId;
    public float ProjectileSpeedBase => projectileSpeedBase; // allow <=0 = unused
        public int BuildCost => buildCost;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float Range => range;
        public DamageType DamageType => damageType;
        public TargetPriority TargetPriority => targetPriority;
        public bool RotateTowardsTarget => rotateTowardsTarget;

        public int MaxLevel
        {
            get
            {
                // Enforce runtime minimum of 10 (balance requirement)
                if (maxLevel < 10)
                {
                    // Do not mutate serialized value silently; just treat as 10 at runtime.
                    return 10;
                }
                return Mathf.Max(10, maxLevel);
            }
        }
        public int UpgradeCostBase => Mathf.Max(0, upgradeCostBase);
        public float UpgradeCostGrowth => Mathf.Max(1f, upgradeCostGrowth);
        public float UpgradeCostExtraMult => Mathf.Max(0.01f, upgradeCostExtraMult);
        public float SellRefundPercent => Mathf.Clamp01(sellRefundPercent);
        public float FireRatePerLevelMult => Mathf.Max(0.01f, fireRatePerLevelMult);
        public float RangePerLevelMult => Mathf.Max(0.01f, rangePerLevelMult);
        public float DamagePerLevelMult => Mathf.Max(0.01f, damagePerLevelMult);
        public float DamageFlatPerLevel => damageFlatPerLevel;

        // Splash getters
        public bool IsSplash => isSplash;
        public float SplashRadiusBase => Mathf.Max(0f, splashRadiusBase);
        public float SplashRadiusPerLevelMult => Mathf.Max(0.01f, splashRadiusPerLevelMult);
        public float SplashRadiusPerLevelFlat => splashRadiusPerLevelFlat;
        public float SplashFalloffExponent => Mathf.Max(0.01f, splashFalloffExponent);

        // Slow getters
        public bool IsSlow => isSlow;
        public float SlowFactorBase => Mathf.Clamp(slowFactorBase, 0.05f, 1f);
        public float SlowFactorPerLevelAdd => slowFactorPerLevelAdd; // applied then clamped
        public float SlowDurationBase => Mathf.Max(0f, slowDurationBase);
        public float SlowDurationPerLevelAdd => slowDurationPerLevelAdd;
        public int ProjectilesPerShotBase => Mathf.Max(1, projectilesPerShotBase);
        public float ProjectilesPerShotPerLevel => Mathf.Max(0f, projectilesPerShotPerLevel);
        public float SlowTowerDamageMultiplier => Mathf.Max(0.01f, slowTowerDamageMultiplier);

        // Sniper getters
        public bool IsSniper => isSniper;
        public float SniperDamageMultiplier => Mathf.Max(0.01f, sniperDamageMultiplier);
    // Negative means infinite. Semantics: TOTAL number of targets that can be damaged. 1 = only first.
    public int SniperPierceCount => sniperPierceCount;
        public float SniperCritChance => Mathf.Clamp01(sniperCritChance);
        public float SniperCritMultiplier => Mathf.Max(1f, sniperCritMultiplier);
        public float SniperRangeMultiplier => Mathf.Max(0.01f, sniperRangeMultiplier);
    public float SniperProjectileSpeed => sniperProjectileSpeed; // allow <=0 = unused
    }
}
