using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.UI; // Added for HUDController toast access

namespace BulletHeavenFortressDefense.Systems
{
    public class PlacementSystem : Singleton<PlacementSystem>, BulletHeavenFortressDefense.Utilities.IRunResettable
    {
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private GameEvent onPlacementSucceeded;
        [SerializeField] private GameEvent onPlacementFailed;

        private TowerData _pendingTower;

        // Public read-only state so UI can avoid selecting immediately after placement
        public bool HasPendingPlacement => _pendingTower != null;
        public float LastPlacementTime { get; private set; } = -999f;

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
                    bool canPlace = mount.CanPlaceTower();
                    visual.SetVisible(true, canPlace);
                }
            }
        }

        private void OnEnable()
        {
            BulletHeavenFortressDefense.Utilities.RunResetRegistry.Register(this);
        }

        private void OnDisable()
        {
            BulletHeavenFortressDefense.Utilities.RunResetRegistry.Unregister(this);
        }

        public void ResetForNewRun()
        {
            _pendingTower = null;
            HideAllSpots();
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

            if (mount == null)
            {
                onPlacementFailed?.Raise();
                // Keep spots visible; user can try another
                return true;
            }

            // Explicit guard: occupied mount should never allow placement; refresh its visual to blocked.
            if (!mount.CanPlaceTower())
            {
                HUDController.Toast("Spot occupied", 1.2f);
                var visualBlocked = mount.GetComponent<Fortress.MountSpotVisual>();
                if (visualBlocked != null)
                {
                    visualBlocked.SetVisible(true, false);
                }
                onPlacementFailed?.Raise();
                // CANCEL pending placement (same behavior as clicking off an empty area)
                _pendingTower = null;
                HideAllSpots();
                return true;
            }

            int baseCost = _pendingTower.BuildCost;
            int scaledCost = Mathf.RoundToInt(baseCost * EconomySystem.Instance.BuildCostGlobalMult);
            if (!EconomySystem.Instance.TrySpend(scaledCost))
            {
                Debug.Log($"[Placement] Not enough energy for {_pendingTower.DisplayName}. Need {scaledCost}, have {EconomySystem.Instance.CurrentEnergy}");
                onPlacementFailed?.Raise();
                return true;
            }

            bool placed = TowerManager.Instance.TryPlaceTowerOnMount(_pendingTower, mount);
            if (placed)
            {
                onPlacementSucceeded?.Raise();
                LastPlacementTime = Time.time;
                _pendingTower = null;
                HideAllSpots();
            }
            else
            {
                // refund scaled cost if failed
                EconomySystem.Instance.Add(scaledCost);
                onPlacementFailed?.Raise();
                // Keep spots visible for another attempt
                RefreshSpotVisuals();
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

        private void RefreshSpotVisuals()
        {
            if (!FortressManager.HasInstance) return;
            foreach (var mount in FortressManager.Instance.Mounts)
            {
                if (mount == null) continue;
                var visual = mount.GetComponent<Fortress.MountSpotVisual>();
                if (visual == null) continue;
                visual.SetVisible(true, mount.CanPlaceTower());
            }
        }
    }
}
