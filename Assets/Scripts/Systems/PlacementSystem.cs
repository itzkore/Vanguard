using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class PlacementSystem : Singleton<PlacementSystem>
    {
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private GameEvent onPlacementSucceeded;
        [SerializeField] private GameEvent onPlacementFailed;

        private TowerData _pendingTower;

        public void QueueTowerPlacement(TowerData towerData)
        {
            _pendingTower = towerData;
        }

        public void HandlePrimaryContact(Vector3 worldPosition)
        {
            if (_pendingTower == null)
            {
                return;
            }

            if (IsPlacementValid(worldPosition))
            {
                bool placed = TowerManager.Instance.TryPlaceTower(_pendingTower, worldPosition);
                if (placed)
                {
                    onPlacementSucceeded?.Raise();
                }
                else
                {
                    onPlacementFailed?.Raise();
                }
            }
            else
            {
                onPlacementFailed?.Raise();
            }
        }

        private bool IsPlacementValid(Vector3 position)
        {
            return !Physics2D.OverlapPoint(position, placementMask);
        }
    }
}
