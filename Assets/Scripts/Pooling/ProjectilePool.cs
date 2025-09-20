using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Pooling
{
    /// <summary>
    /// Lightweight specialized projectile pool sitting beside generic ObjectPoolManager.
    /// Allows code to request a projectile prefab without configuring a full pool table entry.
    /// Intended as a transitional skeleton; can later unify with ObjectPoolManager or DOTS path.
    /// </summary>
    public class ProjectilePool : Singleton<ProjectilePool>, IRunResettable
    {
        [System.Serializable]
        private class Bucket
        {
            public GameObject prefab;
            public readonly Queue<GameObject> free = new();
            public readonly List<GameObject> all = new();
            public int prewarm;
            public bool expandable = true;
        }

        [SerializeField, Tooltip("Optional pre-configured projectile prefabs to warm.")] private List<GameObject> prewarmPrefabs = new();
        [SerializeField, Tooltip("Instances per prewarm prefab to allocate on Awake.")] private int defaultPrewarmCount = 32;
        [SerializeField] private bool verboseLogging = false;

        private readonly Dictionary<GameObject, Bucket> _buckets = new();

        public struct PoolStats
        {
            public int bucketCount;
            public int totalInstances;
            public int totalFree;
            public int totalActive => totalInstances - totalFree;
        }

        public PoolStats GetStats()
        {
            PoolStats s = new PoolStats();
            s.bucketCount = _buckets.Count;
            foreach (var kvp in _buckets)
            {
                var b = kvp.Value;
                s.totalInstances += b.all.Count;
                s.totalFree += b.free.Count;
            }
            return s;
        }

        protected override void Awake()
        {
            base.Awake();
            PrewarmConfigured();
        }

        private void PrewarmConfigured()
        {
            for (int i = 0; i < prewarmPrefabs.Count; i++)
            {
                var pf = prewarmPrefabs[i];
                if (pf == null) continue;
                EnsureBucket(pf, defaultPrewarmCount);
            }
        }

        private Bucket EnsureBucket(GameObject prefab, int additionalWarm = 0)
        {
            if (prefab == null) return null;
            if (!_buckets.TryGetValue(prefab, out var bucket))
            {
                bucket = new Bucket { prefab = prefab, prewarm = additionalWarm };
                _buckets[prefab] = bucket;
                if (verboseLogging) Debug.Log($"[ProjectilePool] Created bucket for {prefab.name}");
            }
            for (int i = 0; i < additionalWarm; i++)
            {
                var inst = CreateInstance(bucket);
                bucket.free.Enqueue(inst);
            }
            return bucket;
        }

        private GameObject CreateInstance(Bucket bucket)
        {
            var inst = Instantiate(bucket.prefab, transform);
            inst.SetActive(false);
            bucket.all.Add(inst);
            // Optionally enforce ITowerProjectile presence
            if (inst.GetComponent<ITowerProjectile>() == null)
            {
                // Do not auto-add; log only to preserve design consistency
                if (verboseLogging) Debug.LogWarning($"[ProjectilePool] Prefab {bucket.prefab.name} has no ITowerProjectile component.");
            }
            return inst;
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!HasInstance)
            {
                Debug.LogWarning("[ProjectilePool] No instance available – instantiate fallback.");
                return Instantiate(prefab, position, rotation);
            }
            return Instance.InternalGet(prefab, position, rotation);
        }

        private GameObject InternalGet(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var bucket = EnsureBucket(prefab, 0);
            if (bucket == null) return null;
            if (bucket.free.Count == 0)
            {
                if (bucket.expandable)
                {
                    bucket.free.Enqueue(CreateInstance(bucket));
                }
                else
                {
                    if (verboseLogging) Debug.LogWarning($"[ProjectilePool] Bucket for {prefab.name} exhausted (expand disabled). Returning null.");
                    return null;
                }
            }
            var go = bucket.free.Dequeue();
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }

        public static void Release(GameObject instance)
        {
            if (instance == null) return;
            if (!HasInstance)
            {
                Destroy(instance);
                return;
            }
            Instance.InternalRelease(instance);
        }

        private void InternalRelease(GameObject instance)
        {
            // Reverse lookup by stored original prefab reference marker?
            // For simplicity skeleton: linear search (acceptable early). Can optimize with component marker later.
            foreach (var kvp in _buckets)
            {
                var bucket = kvp.Value;
                if (bucket.all.Contains(instance))
                {
                    instance.SetActive(false);
                    instance.transform.SetParent(transform);
                    bucket.free.Enqueue(instance);
                    return;
                }
            }
            // Not found -> destroy to avoid leak of detached object
            Destroy(instance);
        }

        public void ResetForNewRun()
        {
            foreach (var kvp in _buckets)
            {
                var bucket = kvp.Value;
                for (int i = 0; i < bucket.all.Count; i++)
                {
                    var go = bucket.all[i];
                    if (go == null) continue;
                    go.SetActive(false);
                    if (!bucket.free.Contains(go)) bucket.free.Enqueue(go);
                }
            }
        }
    }
}
