using UnityEngine;

namespace BulletHeavenFortressDefense.Bootstrap
{
    /// <summary>
    /// Sets appropriate EnemyPace.SpeedMultiplier based on platform so že hra není příliš pomalá na mobile.
    /// Place this on an always-present GameObject in the first loaded scene.
    /// </summary>
    public class PlatformPaceBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Override multiplier specifically for mobile platforms (Android/iOS). 1 = normal speed.")] private float mobileEnemySpeedMultiplier = 1f;
        [SerializeField, Tooltip("Multiplier for standalone/desktop builds (optional). -1 keeps existing.")] private float desktopEnemySpeedMultiplier = -1f;
        [SerializeField, Tooltip("Log applied multiplier on start.")] private bool verbose = true;

        private void Awake()
        {
            // Runtime branch so both serialized fields are "used" on every platform (prevents CS0414 warning in platform-specific builds)
            float current = BulletHeavenFortressDefense.Entities.EnemyPace.SpeedMultiplier;
            float desired = current;

            if (Application.isMobilePlatform)
            {
                desired = Mathf.Max(0.05f, mobileEnemySpeedMultiplier);
                if (verbose) Debug.Log("[PlatformPaceBootstrap] Applied mobile enemy speed multiplier: " + desired);
            }
            else if (desktopEnemySpeedMultiplier > 0f)
            {
                desired = Mathf.Max(0.05f, desktopEnemySpeedMultiplier);
                if (verbose) Debug.Log("[PlatformPaceBootstrap] Applied desktop enemy speed multiplier: " + desired);
            }
            else if (verbose)
            {
                Debug.Log("[PlatformPaceBootstrap] Leaving existing enemy speed multiplier: " + current);
            }

            if (!Mathf.Approximately(desired, current))
            {
                BulletHeavenFortressDefense.Entities.EnemyPace.SpeedMultiplier = desired;
            }
        }
    }
}
