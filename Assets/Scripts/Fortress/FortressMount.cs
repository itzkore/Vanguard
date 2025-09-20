using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Fortress
{
    [RequireComponent(typeof(Collider2D))]
    public class FortressMount : MonoBehaviour
    {
        [SerializeField] private Collider2D interactionCollider;

        private FortressWall _owner;
        private TowerBehaviour _currentTower;
        private bool _available = true;
        private int _row;
        private int _column;

        public FortressWall Owner => _owner;
        public TowerBehaviour CurrentTower => _currentTower;
        public bool HasTower => _currentTower != null;
        public bool Available => _available && _owner != null && _owner.IsAlive;

        private void Awake()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider2D>();
            }
        }

        public void Initialize(FortressWall owner, int row, int column)
        {
            _owner = owner;
            _row = row;
            _column = column;
            _available = owner != null && owner.IsAlive;
            if (FortressManager.HasInstance)
            {
                FortressManager.Instance.RegisterMount(this);
            }
        }

        private void OnDestroy()
        {
            // Avoid noisy error spam when quitting play mode / scene reload where manager already destroyed.
            if (FortressManager.HasInstance)
            {
                FortressManager.Instance.UnregisterMount(this);
            }
        }

        public bool ContainsPoint(Vector2 worldPoint)
        {
            if (interactionCollider == null)
            {
                return false;
            }

            return interactionCollider.OverlapPoint(worldPoint);
        }

        public bool CanPlaceTower()
        {
            return Available && _currentTower == null;
        }

        public void AttachTower(TowerBehaviour tower)
        {
            _currentTower = tower;
            _available = false;
            tower?.AssignMount(this);
        }

        public void NotifyTowerDestroyed(TowerBehaviour tower)
        {
            if (_currentTower == tower)
            {
                _currentTower = null;
                if (_owner != null && _owner.IsAlive)
                {
                    _available = true;
                }
            }
        }

        public void DestroyMountedTower()
        {
            if (_currentTower == null)
            {
                return;
            }

            var tower = _currentTower;
            TowerManager.Instance?.RemoveTower(tower);
        }

        public void SetAvailable(bool available)
        {
            if (_owner != null && !_owner.IsAlive)
            {
                _available = false;
                return;
            }

            _available = available && _currentTower == null;
        }

        public Vector3 GetPlacementPosition()
        {
            return transform.position;
        }

        public float GetSpotSize()
        {
            if (TryGetComponent<MountSpotVisual>(out var vis))
            {
                return vis.Size;
            }
            return 0.46f;
        }

        private void OnDrawGizmosSelected()
        {
            var prev = Gizmos.color;
            Gizmos.color = Available ? Color.green : Color.red;

            Vector3 center = interactionCollider != null ? (Vector3)interactionCollider.bounds.center : transform.position;
            float radius = 0.35f;

            if (interactionCollider != null)
            {
                if (interactionCollider is CircleCollider2D cc)
                {
                    radius = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                }
                else
                {
                    var ext = interactionCollider.bounds.extents;
                    radius = Mathf.Max(ext.x, ext.y);
                }
            }

            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = prev;
        }
    }
}
