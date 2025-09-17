using UnityEngine;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    public class TowerBehaviour : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown;

        private TowerData _data;
        private float _cooldownTimer;

        public void Initialize(TowerData data)
        {
            _data = data;
            fireCooldown = data?.FireRate > 0f ? 1f / data.FireRate : 1f;
        }

        private void Update()
        {
            if (_data == null)
            {
                return;
            }

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                Fire();
                _cooldownTimer = fireCooldown;
            }
        }

        private void Fire()
        {
            if (_data.ProjectilePrefab == null || muzzle == null)
            {
                return;
            }

            var projectileObj = Instantiate(_data.ProjectilePrefab, muzzle.position, muzzle.rotation);
            if (projectileObj.TryGetComponent(out Projectile projectile))
            {
                projectile.Initialize(_data);
            }
        }
    }
}
