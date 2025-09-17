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
        private float _rangeSquared;
        private EnemyController _currentTarget;

        public void Initialize(TowerData data)
        {
            _data = data;
            fireCooldown = data?.FireRate > 0f ? 1f / data.FireRate : 1f;
            _rangeSquared = data != null ? data.Range * data.Range : 0f;
        }

        private void Update()
        {
            if (_data == null)
            {
                return;
            }

            AcquireTarget();
            if (_currentTarget == null)
            {
                return;
            }

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                FireAtTarget();
                _cooldownTimer = fireCooldown;
            }
        }

        private void AcquireTarget()
        {
            if (_currentTarget != null)
            {
                if (!_currentTarget.IsAlive || ( _currentTarget.Position - transform.position).sqrMagnitude > _rangeSquared)
                {
                    _currentTarget = null;
                }
            }

            if (_currentTarget != null)
            {
                return;
            }

            float bestDistance = _rangeSquared;
            foreach (var enemy in EnemyController.ActiveEnemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float distance = (enemy.Position - transform.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    _currentTarget = enemy;
                }
            }
        }

        private void FireAtTarget()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                return;
            }

            if (_data.ProjectilePrefab == null || muzzle == null)
            {
                return;
            }

            Vector3 direction = (_currentTarget.Position - muzzle.position);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            var projectileObj = Instantiate(_data.ProjectilePrefab, muzzle.position, Quaternion.identity);
            if (projectileObj.TryGetComponent(out Projectile projectile))
            {
                projectile.Initialize(_data, direction.normalized);
            }
        }
    }
}
