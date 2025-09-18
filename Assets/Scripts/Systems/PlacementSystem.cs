using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Fortress;

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
            // Toggle spot visuals ON for all mounts based on availability
            if (FortressManager.HasInstance)
            {
                foreach (var mount in FortressManager.Instance.Mounts)
                {
                    if (mount == null) continue;
                    var visual = mount.GetComponent<Fortress.MountSpotVisual>();
                    if (visual == null) visual = mount.gameObject.AddComponent<Fortress.MountSpotVisual>();
                    visual.SetVisible(true, mount.CanPlaceTower());
                }
            }
        }

        public void HandlePrimaryContact(Vector3 worldPosition)
        {
            if (_pendingTower == null)
            {
                return;
            }

            if (TryPlaceOnMount(worldPosition))
            {
                // Hide all spot visuals after a mount placement attempt (success or fail handled inside)
                HideAllSpots();
                return;
            }

            // Ground placement disabled: must place on a mount
            onPlacementFailed?.Raise();
            HideAllSpots();
        }

        private bool TryPlaceOnMount(Vector3 worldPosition)
        {
            if (!FortressManager.HasInstance)
            {
                return false;
            }

            if (!FortressManager.Instance.TryGetMountAt(worldPosition, out var mount))
            {
                return false;
            }

            if (mount == null || !mount.CanPlaceTower())
            {
                onPlacementFailed?.Raise();
                // Keep spots visible; user can try another
                return true;
            }

            if (!EconomySystem.Instance.TrySpend(_pendingTower.BuildCost))
            {
                onPlacementFailed?.Raise();
                return true;
            }

            bool placed = TowerManager.Instance.TryPlaceTowerOnMount(_pendingTower, mount);
            if (placed)
            {
                onPlacementSucceeded?.Raise();
                _pendingTower = null;
                HideAllSpots();
            }
            else
            {
                EconomySystem.Instance.Add(_pendingTower.BuildCost);
                onPlacementFailed?.Raise();
                // Keep spots visible for another attempt
            }

            return true;
        }

        private bool IsPlacementValid(Vector3 position)
        {
            return !Physics2D.OverlapPoint(position, placementMask);
        }

        private void HideAllSpots()
        {
            if (!FortressManager.HasInstance) return;
            foreach (var mount in FortressManager.Instance.Mounts)
            {
                if (mount == null) continue;
                var visual = mount.GetComponent<Fortress.MountSpotVisual>();
                if (visual != null) visual.SetVisible(false, false);
            }
        }
    }
}
