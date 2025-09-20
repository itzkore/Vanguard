using UnityEngine;

namespace BulletHeavenFortressDefense.Pooling
{
    /// <summary>
    /// Simple component for pooled one-shot VFX objects (particles / decals). The effect auto-disables after duration
    /// and returns itself to the owning pool.
    /// </summary>
    public class PooledEffect : MonoBehaviour
    {
        public System.Action<PooledEffect> Return;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private bool deactivateOnReturn = true;
        private float _t;
        private bool _active;

        public void Play(float overrideLifetime = -1f)
        {
            if (overrideLifetime > 0f) lifetime = overrideLifetime;
            _t = 0f; _active = true; enabled = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            // restart particle system(s)
            var ps = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < ps.Length; i++)
            {
                ps[i].Clear(true);
                ps[i].Play(true);
            }
        }

        private void Update()
        {
            if (!_active) return;
            _t += Time.deltaTime;
            if (_t >= lifetime)
            {
                _active = false;
                Return?.Invoke(this);
                if (deactivateOnReturn) gameObject.SetActive(false);
            }
        }
    }
}
