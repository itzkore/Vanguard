using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    public enum EnemyVariant { Base, Fast, Giant }

    [System.Serializable]
    public struct EnemyVariantRule
    {
        public EnemyVariant variant;
        [Range(0.05f, 10f)] public float healthMult;
        [Range(0.05f, 10f)] public float speedMult;
        [Range(0.05f, 10f)] public float contactDamageMult;
        [Range(0.05f, 10f)] public float rewardMult;
        [Range(0.1f, 5f)] public float scaleMult;
        [Tooltip("Optional tint color (alpha ignored; set a>0 to apply)")] public Color tint;
        [Tooltip("Relative spawn weight; 0 disables variant.")] public float spawnWeight;
        [Tooltip("Wave from which this variant becomes eligible (inclusive). 1 = from start")] public int minWave;
    }

    [CreateAssetMenu(fileName = "EnemyVariantConfig", menuName = "BHFD/Config/Enemy Variant Config")] 
    public class EnemyVariantConfig : ScriptableObject
    {
        [SerializeField] private EnemyVariantRule[] rules;

        public EnemyVariantRule[] Rules => rules;

        public bool TryGetRule(EnemyVariant v, out EnemyVariantRule rule)
        {
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    if (rules[i].variant == v)
                    {
                        rule = rules[i];
                        return true;
                    }
                }
            }
            rule = default;
            return false;
        }

        public EnemyVariantRule GetWeightedRandomRule(int currentWave)
        {
            // Aggregate weights for variants allowed at this wave
            float total = 0f;
            if (rules == null || rules.Length == 0)
            {
                return new EnemyVariantRule { variant = EnemyVariant.Base, healthMult = 1f, speedMult = 1f, contactDamageMult = 1f, rewardMult = 1f, scaleMult = 1f, tint = Color.white, spawnWeight = 1f, minWave = 1 };
            }
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r.spawnWeight <= 0f) continue;
                if (currentWave < Mathf.Max(1, r.minWave)) continue;
                total += r.spawnWeight;
            }
            if (total <= 0f)
            {
                // Fallback to base
                EnemyVariantRule baseRule;
                if (TryGetRule(EnemyVariant.Base, out baseRule)) return baseRule;
                return new EnemyVariantRule { variant = EnemyVariant.Base, healthMult = 1f, speedMult = 1f, contactDamageMult = 1f, rewardMult = 1f, scaleMult = 1f, tint = Color.white, spawnWeight = 1f, minWave = 1 };
            }
            float pick = Random.value * total;
            float accum = 0f;
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r.spawnWeight <= 0f) continue;
                if (currentWave < Mathf.Max(1, r.minWave)) continue;
                accum += r.spawnWeight;
                if (pick <= accum) return r;
            }
            // Fallback final
            return rules[0];
        }
    }
}
