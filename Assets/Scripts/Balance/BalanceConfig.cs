using UnityEngine;

namespace BulletHeavenFortressDefense.Balance
{
    public static class BalanceConfig
    {
        public const int TotalWaves = 100;
        public const int FirstWaveEnemyCount = 30;
    // Enemy count now scales multiplicatively: each wave = previous * EnemyCountWaveMultiplier (rounded up)
    public const float EnemyCountWaveMultiplier = 1.5f; // +50% per wave
    // HP scaling still step-based (every N waves) unless changed; leaving existing pattern
    public const int WavesPerHpStep = 10;
    public const float HpStepMultiplier = 1.2f; // +20% HP every 10 waves

        public static int GetEnemyCountForWave(int wave)
        {
            if (wave < 1) wave = 1;
            if (wave == 1) return FirstWaveEnemyCount;
            // geometric progression:  N_w = ceil(N_1 * multiplier^(w-1))
            double exact = FirstWaveEnemyCount * System.Math.Pow(EnemyCountWaveMultiplier, wave - 1);
            int count = Mathf.CeilToInt((float)exact);
            return count;
        }

        public static float GetEnemyHpMultiplierForWave(int wave)
        {
            if (wave < 1) return 1f;
            int decades = (wave - 1) / WavesPerHpStep;
            return Mathf.Pow(HpStepMultiplier, decades);
        }
    }
}
