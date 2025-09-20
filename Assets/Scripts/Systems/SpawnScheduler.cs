using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities; // for Singleton<>

namespace BulletHeavenFortressDefense.Systems
{
    /// <summary>
    /// Centralized enemy spawn scheduler implementing a token-bucket (leaky bucket) rate limiter
    /// to keep total instantiation cost smooth. Supports pattern modules generating positions.
    /// </summary>
    public class SpawnScheduler : Singleton<SpawnScheduler>
    {
        [Header("Rate Limit")]
        [Tooltip("Maximum enemies allowed per second (global hard cap).")]
        [SerializeField] private int maxPerSecond = 100; // requirement
        [SerializeField, Tooltip("Soft ramp-up duration when a large batch arrives (seconds). 0 = instant full rate.")] private float warmupSeconds = 0.5f;
        [SerializeField, Tooltip("If true, use unscaled time for spawning (ignores slow-mo)." )] private bool useUnscaledTime = false;
        [Header("Scheduling")]
        [SerializeField, Tooltip("Maximum number of spawn attempts processed per frame (after rate limiting)." )] private int frameBurstCeiling = 32;
        [SerializeField, Tooltip("Enable verbose scheduler logs.")] private bool verbose = false;

        private struct PendingSpawn
        {
            public EnemyData data;
            public Vector3 position;
            public int laneId;
            public System.Action<EnemyController> onSpawned;
        }

        private readonly Queue<PendingSpawn> _queue = new();

        private float _tokens; // available spawn tokens
        private float _tokenRegenPerSecond;
        private float _warmupTimer;
        private bool _hadLargeBatch;
    private int _spawnedThisSecond;
    private float _secondAccumulator;
    private float _lastRealizedPerSecond;

        public int Pending => _queue.Count;
        public int MaxPerSecond => maxPerSecond;
        public float FillPercent => Mathf.Clamp01(_tokens / Mathf.Max(1f, maxPerSecond));

        protected override void Awake()
        {
            base.Awake();
            _tokenRegenPerSecond = maxPerSecond;
            _tokens = maxPerSecond; // start full so first wave is snappy
        }

        private void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            // Regenerate tokens
            _tokens += _tokenRegenPerSecond * dt;
            float cap = maxPerSecond * Mathf.Clamp01((_hadLargeBatch && warmupSeconds > 0f) ? Mathf.Clamp01((_tokenRegenPerSecond * (_warmupTimer += dt)) / (maxPerSecond * warmupSeconds)) : 1f);
            if (_tokens > cap) _tokens = cap;
            if (_tokens > maxPerSecond) _tokens = maxPerSecond;

            int processed = 0;
            while (_queue.Count > 0 && _tokens >= 1f && processed < frameBurstCeiling)
            {
                _tokens -= 1f;
                var ps = _queue.Dequeue();
                EnemyController enemy = null;
                if (ps.laneId >= 0)
                {
                    enemy = SpawnSystem.HasInstance ? SpawnSystem.Instance.SpawnEnemy(ps.data, ps.laneId) : null;
                }
                else
                {
                    if (SpawnSystem.HasInstance)
                    {
                        enemy = SpawnSystem.Instance.SpawnEnemyAtPosition(ps.data, ps.position);
                    }
                }
                ps.onSpawned?.Invoke(enemy);
                processed++;
                _spawnedThisSecond++;
            }

            if (verbose && processed > 0)
            {
                Debug.Log($"[SpawnScheduler] Processed {processed} spawns this frame. Tokens={_tokens:F1} Pending={_queue.Count}");
            }

            _secondAccumulator += dt;
            if (_secondAccumulator >= 1f)
            {
                _lastRealizedPerSecond = _spawnedThisSecond / _secondAccumulator;
                _spawnedThisSecond = 0;
                _secondAccumulator = 0f;
            }
        }

        public void EnqueuePosition(EnemyData data, Vector3 position, System.Action<EnemyController> onSpawned)
        {
            _queue.Enqueue(new PendingSpawn{ data=data, position=position, laneId=-1, onSpawned=onSpawned});
            if (_queue.Count > maxPerSecond && !_hadLargeBatch) { _hadLargeBatch = true; _warmupTimer = 0f; }
        }

        public void EnqueueLane(EnemyData data, int laneId, System.Action<EnemyController> onSpawned)
        {
            _queue.Enqueue(new PendingSpawn{ data=data, laneId=laneId, position=Vector3.zero, onSpawned=onSpawned});
            if (_queue.Count > maxPerSecond && !_hadLargeBatch) { _hadLargeBatch = true; _warmupTimer = 0f; }
        }

        public (int pending, float tokens, int maxPerSec) GetStats()
        {
            return (_queue.Count, _tokens, maxPerSecond);
        }

        public float GetRealizedRatePerSecond() => _lastRealizedPerSecond;
    }
}
