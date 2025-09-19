using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Debugging
{
    public class DebugRunStarter : MonoBehaviour
    {
        [SerializeField] private TowerData defaultTower;
        [SerializeField] private float queueInterval = 2f;

        private void Start()
        {
            QueueTower();
            // StartRun already triggers WaveManager.StartSequence(); avoid double-start which was resetting wave number.
            GameManager.Instance.StartRun();
            // Removed explicit WaveManager.Instance.StartSequence();
            if (queueInterval > 0f)
            {
                InvokeRepeating(nameof(QueueTower), queueInterval, queueInterval);
            }
        }

        private void QueueTower()
        {
            if (defaultTower != null && PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.QueueTowerPlacement(defaultTower);
            }
        }
    }
}
