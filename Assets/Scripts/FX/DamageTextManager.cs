using UnityEngine;
using System.Collections.Generic;

namespace BulletHeavenFortressDefense.FX
{
    // Manages pooling & spawning of floating damage texts.
    public class DamageTextManager : MonoBehaviour
    {
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField, Tooltip("Maximum pooled instances (will recycle oldest if exceeded)")] private int poolSize = 64;
        [SerializeField, Tooltip("YOffset above enemy position to spawn text")] private float yOffset = 0.4f;
        [SerializeField, Tooltip("Parent for spawned texts (optional). If null, manager's transform is used.")] private Transform textParent;

        private readonly Queue<DamageText> _pool = new();
        private readonly List<DamageText> _active = new();

        public static DamageTextManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (textParent == null) textParent = transform;
        }

        public static void Show(Vector3 worldPos, float amount, bool crit)
        {
            if (!HasInstance) return;
            Instance.InternalShow(worldPos, amount, crit);
        }

        private void InternalShow(Vector3 worldPos, float amount, bool crit)
        {
            if (damageTextPrefab == null) return; // no prefab assigned
            DamageText dt = GetFromPool();
            Vector3 spawnPos = worldPos + new Vector3(0f, yOffset, 0f);
            dt.Show(spawnPos, amount, crit);
        }

        private DamageText GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var dt = _pool.Dequeue();
                _active.Add(dt);
                dt.gameObject.SetActive(true);
                return dt;
            }
            // Need new instance
            if (_active.Count >= poolSize && _active.Count > 0)
            {
                // Recycle oldest
                var recycle = _active[0];
                _active.RemoveAt(0);
                recycle.gameObject.SetActive(false);
                _pool.Enqueue(recycle);
            }
            DamageText created = Instantiate(damageTextPrefab, textParent);
            _active.Add(created);
            created.SetManager(this);
            return created;
        }

        internal void Release(DamageText dt)
        {
            if (dt == null) return;
            if (_active.Remove(dt))
            {
                dt.gameObject.SetActive(false);
                _pool.Enqueue(dt);
            }
        }
    }
}
