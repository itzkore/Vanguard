using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.AI
{
    /// <summary>
    /// Central lightweight coordinator to reduce over-focusing: keeps a count of how many towers are aiming
    /// at each enemy and provides an adjusted score so towers naturally prefer less-contested enemies.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class TargetFocusCoordinator : MonoBehaviour
    {
        public static TargetFocusCoordinator Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        [Tooltip("Penalty added per tower already focusing an enemy (higher = stronger distribution)." )]
        public float focusPenalty = 4f;
        [Tooltip("Optional extra penalty multiplier for very low HP enemies already being overkilled.")]
        public float lowHealthPenaltyMult = 1.5f;
        [Tooltip("Health threshold ratio (0-1). If enemy health fraction below -> apply lowHealthPenaltyMult.")]
        [Range(0f,1f)] public float lowHealthRatio = 0.25f;

        private readonly Dictionary<EnemyController, int> _focusCounts = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
        }

        public void NotifyFocus(EnemyController enemy)
        {
            if (enemy == null) return;
            _focusCounts.TryGetValue(enemy, out int c);
            _focusCounts[enemy] = c + 1;
        }

        public void NotifyRelease(EnemyController enemy)
        {
            if (enemy == null) return;
            if (_focusCounts.TryGetValue(enemy, out int c))
            {
                c--; if (c <= 0) _focusCounts.Remove(enemy); else _focusCounts[enemy] = c;
            }
        }

        public int GetFocusCount(EnemyController enemy)
        {
            if (enemy == null) return 0;
            return _focusCounts.TryGetValue(enemy, out int c) ? c : 0;
        }

        /// <summary>
        /// Returns additive score penalty (lower = better) that tower code can add onto its base metric
        /// (distance, remaining health etc.) so that targets already heavily focused become less attractive.
        /// </summary>
        public float GetPenalty(EnemyController enemy)
        {
            if (enemy == null) return 0f;
            int count = GetFocusCount(enemy);
            if (count <= 0) return 0f;
            float penalty = focusPenalty * count;
            float healthFrac = enemy.MaxHealth > 0f ? enemy.RemainingHealth / enemy.MaxHealth : 1f;
            if (healthFrac < lowHealthRatio)
            {
                penalty *= lowHealthPenaltyMult;
            }
            return penalty;
        }

        private void LateUpdate()
        {
            // Cleanup dead enemies to avoid dictionary growth
            _toClean.Clear();
            foreach (var kv in _focusCounts)
            {
                if (kv.Key == null || !kv.Key.IsAlive)
                {
                    _toClean.Add(kv.Key);
                }
            }
            if (_toClean.Count > 0)
            {
                for (int i = 0; i < _toClean.Count; i++) _focusCounts.Remove(_toClean[i]);
            }
        }

        private static readonly List<EnemyController> _toClean = new();
    }
}
