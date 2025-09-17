using System.Collections;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class WaveManager : Singleton<WaveManager>
    {
        [SerializeField] private WaveSequence waveSequence;
        [SerializeField] private float interWaveDelay = 4f;
        [SerializeField] private GameEvent onWaveStarted;
        [SerializeField] private GameEvent onWaveCompleted;

        private int _currentWaveIndex = -1;
        private Coroutine _activeRoutine;

        public bool IsSpawning => _activeRoutine != null;

        public void StartSequence()
        {
            StopActiveRoutine();
            _currentWaveIndex = -1;
            _activeRoutine = StartCoroutine(RunSequence());
        }

        public void StopSequence()
        {
            StopActiveRoutine();
        }

        private IEnumerator RunSequence()
        {
            while (waveSequence != null)
            {
                _currentWaveIndex++;
                if (_currentWaveIndex >= waveSequence.Waves.Count)
                {
                    // Loop for endless mode; otherwise break.
                    _currentWaveIndex = 0;
                }

                var wave = waveSequence.Waves[_currentWaveIndex];
                onWaveStarted?.Raise();
                yield return SpawnWave(wave);
                onWaveCompleted?.Raise();
                yield return new WaitForSeconds(interWaveDelay);
            }
        }

        private IEnumerator SpawnWave(WaveData wave)
        {
            foreach (var entry in wave.Spawns)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    Systems.SpawnSystem.Instance.SpawnEnemy(entry.enemyData, entry.spawnPointId);
                    yield return new WaitForSeconds(entry.spawnInterval);
                }
            }
        }

        private void StopActiveRoutine()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
        }
    }
}
