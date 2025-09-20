using UnityEngine;
using BulletHeavenFortressDefense.Balance;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Bootstrap
{
    /// <summary>
    /// Jednoduchý bootstrap pro rychlé doladění balanc parametrů bez potřeby kódu.
    /// Umísti na GameObject v první scéně (spolu s PlatformPaceBootstrap nebo Preloaderem).
    /// </summary>
    public class BalanceBootstrap : MonoBehaviour
    {
        [Header("Enemy HP (Rapid Base Hits)")]
        [Tooltip("Kolik 'base rapid' zásahů (faktor) definuje základní HP. 2 = původní, 1 = ultra křehké, 3+ = tank.")]
        [Range(0.5f, 5f)] public float rapidBaseHitFactor = 2f;

        [Header("Enemy Pace")]
        [Tooltip("Volitelný override EnemyPace.SpeedMultiplier. <=0 = nezměnit.")]
        public float enemySpeedMultiplier = 0f;

        [Header("Apply Timing")]
        [Tooltip("Aplikovat v Awake (nejdřív) nebo Start (bezpečně po jiných Awakes)." )]
        public bool applyInAwake = true;

        private bool _applied;

        private void Awake()
        {
            if (applyInAwake) Apply();
        }
        private void Start()
        {
            if (!_applied && !applyInAwake) Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            EnemyDynamicBalance.RapidBaseHitFactor = rapidBaseHitFactor;
            if (enemySpeedMultiplier > 0f)
            {
                EnemyPace.SpeedMultiplier = enemySpeedMultiplier;
            }
            _applied = true;
            Debug.Log($"[BalanceBootstrap] Applied RapidBaseHitFactor={EnemyDynamicBalance.RapidBaseHitFactor}, EnemySpeedMultiplier={EnemyPace.SpeedMultiplier}");
        }
    }
}
