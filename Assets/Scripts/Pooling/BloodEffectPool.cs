using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Pooling
{
    /// <summary>
    /// Pool for reusable blood / hit VFX. Assign a prefab (with PooledEffect + particle systems) in inspector.
    /// Use BloodEffectPool.Spawn(position, rotation) to play.
    /// </summary>
    public class BloodEffectPool : Singleton<BloodEffectPool>, IRunResettable
    {
        [SerializeField] private PooledEffect prefab;
        [SerializeField] private int prewarm = 64;
        [SerializeField] private int hardCap = 2048; // safety to avoid runaway allocation
        [SerializeField] private bool verboseLogging = false;

        private readonly Queue<PooledEffect> _pool = new();
        private readonly List<PooledEffect> _all = new();
        private int _spawnedThisFrame; private int _lastFrame;

        public struct PoolStats
        {
            public int totalInstances;
            public int free;
            public int active => totalInstances - free;
            public int spawnedThisFrame;
            public int hardCap;
        }

        public PoolStats GetStats()
        {
            return new PoolStats
            {
                totalInstances = _all.Count,
                free = _pool.Count,
                spawnedThisFrame = _spawnedThisFrame,
                hardCap = hardCap
            };
        }

        private void Start()
        {
            if (prefab != null)
            {
                for (int i = 0; i < prewarm; i++)
                {
                    AddNewInstance();
                }
            }
        }

        private PooledEffect AddNewInstance()
        {
            if (prefab == null) return null;
            if (_all.Count >= hardCap) return null;
            var inst = Instantiate(prefab, transform);
            inst.gameObject.SetActive(false);
            inst.Return = OnReturn;
            _pool.Enqueue(inst);
            _all.Add(inst);
            return inst;
        }

        private void OnReturn(PooledEffect fx)
        {
            if (fx == null) return;
            _pool.Enqueue(fx);
        }

        public static void Spawn(Vector3 position, Quaternion rotation, float overrideLifetime = -1f)
        {
            if (!HasInstance)
            {
                Debug.LogWarning("[BloodEffectPool] No instance in scene.");
                return;
            }
            Instance.InternalSpawn(position, rotation, overrideLifetime);
        }

        private void InternalSpawn(Vector3 position, Quaternion rotation, float overrideLifetime)
        {
            if (prefab == null)
            {
                if (verboseLogging) Debug.LogWarning("[BloodEffectPool] Prefab missing.");
                return;
            }
            if (_pool.Count == 0)
            {
                AddNewInstance();
            }
            if (_pool.Count == 0) return; // still none (cap reached)
            var fx = _pool.Dequeue();
            fx.transform.SetPositionAndRotation(position, rotation);
            fx.Play(overrideLifetime);
            _spawnedThisFrame++;
        }

        private void LateUpdate()
        {
            if (_lastFrame != Time.frameCount)
            {
                _lastFrame = Time.frameCount;
                _spawnedThisFrame = 0;
            }
        }

        public void ResetForNewRun()
        {
            // Just deactivate all active effects & return them.
            for (int i = 0; i < _all.Count; i++)
            {
                var fx = _all[i];
                if (fx == null) continue;
                fx.gameObject.SetActive(false);
                if (!_pool.Contains(fx)) _pool.Enqueue(fx);
            }
        }
    }
}
