using UnityEngine;

namespace BulletHeavenFortressDefense.Entities
{
    /// <summary>
    /// Central place to configure a global speed multiplier that ONLY affects enemies.
    /// Keep this separate from Time.timeScale so UI, projectiles, towers, FX, etc. remain at normal speed.
    /// </summary>
    public static class EnemyPace
    {
        /// <summary>
        /// Global enemy speed multiplier. 1 = normal. 0.5 = half speed (slower movement & attack cadence).
        /// Values clamped to a small positive range to avoid freezing logic or exploding speeds.
        /// </summary>
        public static float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Clamp(value, 0.05f, 5f);
        }
        private static float _speedMultiplier = 0.5f; // default: half speed as per revised requirement (enemies only)

        /// <summary>
        /// Returns scaled deltaTime for enemy movement / attack logic.
        /// Using a method keeps call-sites explicit and allows future switch to unscaled time if needed.
        /// </summary>
        public static float EnemyDeltaTime => Time.deltaTime * _speedMultiplier;
    }
}
