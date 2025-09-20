using UnityEngine;
using BulletHeavenFortressDefense.Systems;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Testing
{
    /// <summary>
    /// Simple runtime stress helper: press F9 to queue a large artificial batch of enemies
    /// through the SpawnScheduler to validate rate limiting & smoothness.
    /// </summary>
    public class SpawnStressTest : MonoBehaviour
    {
        [SerializeField] private EnemyData enemyData;
        [SerializeField, Min(1)] private int count = 1000;
        [SerializeField, Range(0f,1f)] private float verticalPadding = 0.05f;
        [SerializeField] private bool autoAttachIfMissing = true;
        [SerializeField] private KeyCode triggerKey = KeyCode.F9;

        private void Awake()
        {
            if (enemyData == null && autoAttachIfMissing && WaveManager.HasInstance)
            {
                enemyData = WaveManager.Instance.DefaultEnemyData;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(triggerKey))
            {
                if (enemyData == null || !SpawnScheduler.HasInstance || !SpawnSystem.HasInstance)
                {
                    Debug.LogWarning("[SpawnStressTest] Missing dependencies.");
                    return;
                }
                float hMin = verticalPadding;
                float hMax = 1f - verticalPadding;
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count;
                    float ny = Mathf.Lerp(hMin, hMax, t);
                    var pos = SpawnSystem.Instance.GetRightEdgePositionAtNormalizedY(ny);
                    pos.x += 0.25f; // slight offset from edge logic
                    SpawnScheduler.Instance.EnqueuePosition(enemyData, pos, _ => { });
                }
                Debug.Log($"[SpawnStressTest] Enqueued {count} enemies for stress validation.");
            }
        }
    }
}
