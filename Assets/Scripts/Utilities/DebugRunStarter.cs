using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Debugging
{
    public class DebugRunStarter : MonoBehaviour
    {
        [SerializeField] private TowerData defaultTower;

        private void Start()
        {
            if (defaultTower != null)
            {
                PlacementSystem.Instance.QueueTowerPlacement(defaultTower);
            }

            GameManager.Instance.StartRun();
            WaveManager.Instance.StartSequence();
        }
    }
}
