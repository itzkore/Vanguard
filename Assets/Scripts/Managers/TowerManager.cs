using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class TowerManager : Singleton<TowerManager>
    {
        [SerializeField] private List<TowerData> unlockedTowers = new List<TowerData>();
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private float placementRadius = 0.45f;
        [Header("Rendering")]
        [SerializeField] private int towerSortingOrder = 5; // ensure towers render above walls (order 0)

        private readonly List<Entities.TowerBehaviour> _activeTowers = new();

        public IReadOnlyList<TowerData> UnlockedTowers => unlockedTowers;

        public bool TryPlaceTower(TowerData towerData, Vector3 targetPosition)
        {
            if (towerData == null)
            {
                return false;
            }

            if (!CanPlace(targetPosition))
            {
                return false;
            }

            var tower = Systems.SpawnSystem.Instance.SpawnTower(towerData, targetPosition);
            if (tower != null)
            {
                ConfigureTowerRendering(tower);
                _activeTowers.Add(tower);
                return true;
            }

            return false;
        }

        public bool TryPlaceTowerOnMount(TowerData towerData, FortressMount mount)
        {
            if (towerData == null || mount == null || !mount.CanPlaceTower())
            {
                return false;
            }

            var tower = Systems.SpawnSystem.Instance.SpawnTower(towerData, mount.GetPlacementPosition(), mount.transform);
            if (tower == null)
            {
                return false;
            }

            tower.transform.localPosition = Vector3.zero;
            // Scale tower to fit the mount square exactly using sprite bounds
            FitTowerToMount(tower, mount.GetSpotSize());
            ConfigureTowerRendering(tower);
            mount.AttachTower(tower);
            _activeTowers.Add(tower);
            return true;
        }

        public void RemoveTower(Entities.TowerBehaviour tower)
        {
            if (tower == null)
            {
                return;
            }

            _activeTowers.Remove(tower);
            if (tower.gameObject != null)
            {
                Object.Destroy(tower.gameObject);
            }
        }

        private bool CanPlace(Vector3 position)
        {
            return Physics2D.OverlapCircle(position, placementRadius, placementMask) == null;
        }

        private void ConfigureTowerRendering(Entities.TowerBehaviour tower)
        {
            if (tower == null) return;

            // Ensure Z = 0 so sorting by order is consistent in 2D
            var pos = tower.transform.position;
            tower.transform.position = new Vector3(pos.x, pos.y, 0f);

            // Enable and bump sorting order on all SpriteRenderers so tower renders above walls (sortingOrder 0)
            var renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.enabled = true;
                if (r.sortingOrder < towerSortingOrder)
                {
                    r.sortingOrder = towerSortingOrder;
                }
            }
        }

        private void FitTowerToMount(Entities.TowerBehaviour tower, float mountSize)
        {
            if (tower == null) return;
            mountSize = Mathf.Max(0.01f, mountSize);

            // Collect bounds from all SpriteRenderers to determine the largest local sprite size
            var renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                tower.transform.localScale = new Vector3(mountSize, mountSize, 1f);
                return;
            }

            // Compute the maximum dimension of the combined sprite bounds in local space assuming initial scale
            // Use renderer.sprite.bounds (local space) length to scale uniformly to fit mount square
            float maxDim = 0.001f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sprite == null) continue;
                var b = r.sprite.bounds; // local units before scaling
                maxDim = Mathf.Max(maxDim, b.size.x, b.size.y);
            }

            float uniformScale = mountSize / maxDim;
            tower.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
    }
}
