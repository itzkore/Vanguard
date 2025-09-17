using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class ObjectPoolManager : Singleton<ObjectPoolManager>
    {
        [System.Serializable]
        public class PoolConfig
        {
            public string id;
            public GameObject prefab;
            public int size = 10;
        }

        [SerializeField] private List<PoolConfig> pools = new();

        private readonly Dictionary<string, Queue<GameObject>> _poolLookup = new();

        private void Start()
        {
            foreach (var pool in pools)
            {
                if (pool.prefab == null || string.IsNullOrEmpty(pool.id))
                {
                    continue;
                }

                var queue = new Queue<GameObject>();
                for (int i = 0; i < Mathf.Max(1, pool.size); i++)
                {
                    var obj = Instantiate(pool.prefab);
                    obj.SetActive(false);
                    queue.Enqueue(obj);
                }

                _poolLookup[pool.id] = queue;
            }
        }

        public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation)
        {
            if (!_poolLookup.TryGetValue(poolId, out var queue) || queue.Count == 0)
            {
                Debug.LogWarning($"Pool {poolId} exhausted or missing.");
                return null;
            }

            var instance = queue.Dequeue();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            queue.Enqueue(instance);
            return instance;
        }
    }
}
