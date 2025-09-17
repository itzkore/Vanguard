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
        [SerializeField] private int buildCost = 10;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private float range = 3f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private TargetPriority targetPriority = TargetPriority.ClosestToTower;
        [SerializeField] private bool rotateTowardsTarget = true;

        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public string ProjectilePoolId => projectilePoolId;
        public int BuildCost => buildCost;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float Range => range;
        public DamageType DamageType => damageType;
        public TargetPriority TargetPriority => targetPriority;
        public bool RotateTowardsTarget => rotateTowardsTarget;
    }
}
