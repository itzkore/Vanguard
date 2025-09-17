using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class TowerManager : Singleton<TowerManager>
    {
        [SerializeField] private List<TowerData> unlockedTowers = new List<TowerData>();
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private float placementRadius = 0.45f;

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
                _activeTowers.Add(tower);
                return true;
            }

            return false;
        }

        public void RemoveTower(Entities.TowerBehaviour tower)
        {
            if (tower == null)
            {
                return;
            }

            _activeTowers.Remove(tower);
            Destroy(tower.gameObject);
        }

        private bool CanPlace(Vector3 position)
        {
            return Physics2D.OverlapCircle(position, placementRadius, placementMask) == null;
        }
    }
}
