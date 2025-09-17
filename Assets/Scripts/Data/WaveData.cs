using System;
using System.Collections.Generic;
using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "BHFD/Data/Wave")]
    public class WaveData : ScriptableObject
    {
        [SerializeField] private List<WaveSpawnEntry> spawns = new();

        public IReadOnlyList<WaveSpawnEntry> Spawns => spawns;
    }

    [Serializable]
    public struct WaveSpawnEntry
    {
        public EnemyData enemyData;
        public int count;
        public float spawnInterval;
        public int spawnPointId;
    }
}
