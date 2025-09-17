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

        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public float Health => health;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public int Reward => reward;
        public DamageResistance[] Resistances => resistances;
        public string PoolId => poolId;

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
