using System;
using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    /// <summary>
    /// Smooths large enemy bursts by rate-limiting instantiation. Enqueue spawn requests instead of spawning
    /// them all in the same frame. Target is to keep frame time stable while still delivering continuous pressure.
    /// </summary>
    public class EnemySpawnBuffer : Singleton<EnemySpawnBuffer>, IRunResettable
    {
        [Header("Rate Settings")] 
        [SerializeField, Tooltip("Desired enemy spawn rate (enemies / second) while backlog exists.")] private float targetRatePerSecond = 80f;
        [SerializeField, Tooltip("Maximum enemies processed in a single frame (hard cap).") ] private int maxPerFrame = 160;
        [SerializeField, Tooltip("Cap on accumulated credits (prevents huge catch-up spikes after long pauses)." )] private float maxCredit = 400f;
        [Header("Adaptive Scaling")]
        [SerializeField, Tooltip("Reduce rate as active enemy count climbs (keeps CPU / draw cost bounded)." )] private bool scaleRateByActiveCount = true;
        [SerializeField, Tooltip("Active enemy count where rate reaches 50% of base.")] private int halfRateActiveCount = 4000;
        [Header("Debug")] 
        [SerializeField] private bool verboseLogging = false;

        private readonly Queue<SpawnRequest> _queue = new();
        private float _credits;
        private int _lastLogFrame;

        private struct SpawnRequest
        {
            public EnemyData data;
            public Vector3 position;
            public int laneSpawnPointId;
            public float rapidBaseDamage;
            public int waveNumber;
            public Action<EnemyController> callback;
            public bool useDirectPosition; // if false -> laneSpawnPointId path
        }

        public int Pending => _queue.Count;

        public void EnqueueDirect(EnemyData data, Vector3 position, float rapidBaseDamage, int waveNumber, Action<EnemyController> onSpawned)
        {
            if (data == null) return;
            _queue.Enqueue(new SpawnRequest
            {
                data = data,
                position = position,
                laneSpawnPointId = -1,
                useDirectPosition = true,
                rapidBaseDamage = rapidBaseDamage,
                waveNumber = waveNumber,
                callback = onSpawned
            });
        }

        public void EnqueueLane(EnemyData data, int laneSpawnPointId, float rapidBaseDamage, int waveNumber, Action<EnemyController> onSpawned)
        {
            if (data == null) return;
            _queue.Enqueue(new SpawnRequest
            {
                data = data,
                laneSpawnPointId = laneSpawnPointId,
                useDirectPosition = false,
                rapidBaseDamage = rapidBaseDamage,
                waveNumber = waveNumber,
                callback = onSpawned
            });
        }

        private void Update()
        {
            if (_queue.Count == 0) return;
            float dt = Time.deltaTime;
            float rate = targetRatePerSecond;
            if (scaleRateByActiveCount && halfRateActiveCount > 0)
            {
                int active = EnemyController.ActiveEnemies != null ? EnemyController.ActiveEnemies.Count : 0;
                if (active > 0)
                {
                    float t = Mathf.Clamp01(active / (float)halfRateActiveCount); // 0..1
                    rate *= Mathf.Lerp(1f, 0.5f, t); // linear to half at halfRateActiveCount
                }
            }
            _credits += rate * dt;
            if (_credits > maxCredit) _credits = maxCredit;

            int spawnedThisFrame = 0;
            while (_credits >= 1f && _queue.Count > 0)
            {
                if (spawnedThisFrame >= maxPerFrame) break;
                _credits -= 1f;
                var req = _queue.Dequeue();
                EnemyController enemy = null;
                if (req.useDirectPosition)
                {
                    if (SpawnSystem.Instance != null)
                        enemy = SpawnSystem.Instance.SpawnEnemyAtPosition(req.data, req.position);
                }
                else
                {
                    if (SpawnSystem.Instance != null)
                        enemy = SpawnSystem.Instance.SpawnEnemy(req.data, req.laneSpawnPointId);
                }
                if (enemy != null)
                {
                    enemy.ApplyBalanceOverrides(req.rapidBaseDamage, req.waveNumber);
                    req.callback?.Invoke(enemy);
                }
                spawnedThisFrame++;
            }

            if (verboseLogging && (spawnedThisFrame > 0 || _lastLogFrame != Time.frameCount))
            {
                _lastLogFrame = Time.frameCount;
                Debug.Log($"[EnemySpawnBuffer] frame={_lastLogFrame} spawned={spawnedThisFrame} pending={_queue.Count} credits={_credits:F2}");
            }
        }

        public void ResetForNewRun()
        {
            _queue.Clear();
            _credits = 0f;
        }

        public void ResetAfterGameOver()
        {
            _queue.Clear();
        }
    }
}
