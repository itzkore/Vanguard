using System;
using System.Collections.Generic;
using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "BHFD/Data/Wave")]
    public class WaveData : ScriptableObject
    {
        [Header("Phase Durations")]
        [SerializeField, Tooltip("Seconds for the shopping phase. Zero or negative values require manual advance.")] private float shopDuration = 25f;
        [SerializeField, Tooltip("Seconds for the preparation phase. Zero or negative values require manual advance.")] private float preparationDuration = 8f;
        [SerializeField, Tooltip("Delay after the last enemy is defeated before the next wave begins.")] private float postCombatDelay = 4f;

        [Header("Enemies")]
        [SerializeField] private List<WaveSpawnEntry> spawns = new();

        public IReadOnlyList<WaveSpawnEntry> Spawns => spawns;
        public float ShopDuration => shopDuration;
        public float PreparationDuration => preparationDuration;
        public float PostCombatDelay => Mathf.Max(0f, postCombatDelay);
        public bool RequiresManualShopAdvance => shopDuration <= 0f;
        public bool RequiresManualPrepAdvance => preparationDuration <= 0f;
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                if (spawns == null)
                {
                    return total;
                }

                for (int i = 0; i < spawns.Count; i++)
                {
                    total += Mathf.Max(0, spawns[i].count);
                }

                return total;
            }
        }

        // Runtime helpers for procedural generation
        public void ClearSpawns() => spawns.Clear();
        public void AddSpawnEntry(WaveSpawnEntry entry) => spawns.Add(entry);
        public void ConfigurePhaseDurations(float shop, float prep, float post)
        {
            shopDuration = shop;
            preparationDuration = prep;
            postCombatDelay = post;
        }
    }

    [Serializable]
    public struct WaveSpawnEntry
    {
        [Tooltip("Enemy data to spawn.")] public EnemyData enemyData;
        [Min(0), Tooltip("Number of enemies to spawn in this entry.")] public int count;
        [Min(0f), Tooltip("Seconds between enemy spawns in this entry.")] public float spawnInterval;
        [Tooltip("Optional lane index. Negative values spawn on a random lane.")] public int spawnPointId;
        [Tooltip("If true, ignores lanes and spawns along the full vertical length at the right screen edge.")] public bool spawnAlongRightEdge;
    }
}
