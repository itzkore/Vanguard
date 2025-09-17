using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class SpawnSystem : Singleton<SpawnSystem>
    {
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] towerSpawnPoints;

        public EnemyController SpawnEnemy(EnemyData enemyData, int spawnPointId)
        {
            if (enemyData?.Prefab == null)
            {
                return null;
            }

            var point = GetSpawnPoint(enemySpawnPoints, spawnPointId);
            GameObject instance = null;

            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, point.position, Quaternion.identity);
            }

            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, point.position, Quaternion.identity);
            }

            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }

            controller.Initialize(enemyData, enemyData.PoolId);
            return controller;
        }

        public TowerBehaviour SpawnTower(TowerData towerData, Vector3 position)
        {
            if (towerData?.Prefab == null)
            {
                return null;
            }

            var instance = Instantiate(towerData.Prefab, position, Quaternion.identity);
            var behaviour = instance.GetComponent<TowerBehaviour>();
            behaviour.Initialize(towerData);
            return behaviour;
        }

        private Transform GetSpawnPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0)
            {
                var fallback = new GameObject("SpawnPoint").transform;
                fallback.position = Vector3.zero;
                return fallback;
            }

            index = Mathf.Clamp(index, 0, points.Length - 1);
            return points[index];
        }
    }
}
