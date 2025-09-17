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
            public bool expandable = true;
        }

        private class PoolRuntime
        {
            public PoolConfig Config;
            public readonly Queue<GameObject> Available = new();
            public readonly List<GameObject> All = new();
        }

        [SerializeField] private List<PoolConfig> pools = new();

        private readonly Dictionary<string, PoolRuntime> _poolLookup = new();

        protected override void Awake()
        {
            base.Awake();
            InitializePools();
        }

        private void InitializePools()
        {
            _poolLookup.Clear();

            foreach (var pool in pools)
            {
                if (pool.prefab == null || string.IsNullOrWhiteSpace(pool.id))
                {
                    continue;
                }

                var runtime = new PoolRuntime { Config = pool };
                int targetCount = Mathf.Max(1, pool.size);
                for (int i = 0; i < targetCount; i++)
                {
                    var instance = CreateInstance(pool.id, pool.prefab, runtime);
                    runtime.Available.Enqueue(instance);
                }

                _poolLookup[pool.id] = runtime;
            }
        }

        public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation)
        {
            if (!_poolLookup.TryGetValue(poolId, out var runtime))
            {
                Debug.LogWarning($"Pool {poolId} not configured.");
                return null;
            }

            if (runtime.Available.Count == 0)
            {
                if (!runtime.Config.expandable)
                {
                    Debug.LogWarning($"Pool {poolId} exhausted (expand disabled).");
                    return null;
                }

                var expanded = CreateInstance(poolId, runtime.Config.prefab, runtime);
                runtime.Available.Enqueue(expanded);
            }

            var instance = runtime.Available.Dequeue();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public void Release(string poolId, GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!_poolLookup.TryGetValue(poolId, out var runtime))
            {
                Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);
            runtime.Available.Enqueue(instance);
        }

        private GameObject CreateInstance(string poolId, GameObject prefab, PoolRuntime runtime)
        {
            var instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            runtime.All.Add(instance);

            var marker = instance.GetComponent<PooledInstanceMarker>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledInstanceMarker>();
            }

            marker.PoolId = poolId;
            return instance;
        }
    }
}
