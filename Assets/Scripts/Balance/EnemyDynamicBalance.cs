using UnityEngine;

namespace BulletHeavenFortressDefense.Balance
{
    /// <summary>
    /// Runtime-tunable enemy balance factors separated from static BalanceConfig progression.
    /// </summary>
    public static class EnemyDynamicBalance
    {
        /// <summary>
        /// Factor of Rapid level 1 damage that defines baseline enemy HP for wave formula. Default 2 (two hits to kill at wave 1).
        /// Lower this to make enemies squishier, raise to make tougher.
        /// </summary>
        public static float RapidBaseHitFactor
        {
            get => _rapidBaseHitFactor;
            set => _rapidBaseHitFactor = Mathf.Clamp(value, 0.25f, 10f);
        }
        private static float _rapidBaseHitFactor = 2f;
    }
}
