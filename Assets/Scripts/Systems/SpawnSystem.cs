using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class SpawnSystem : Singleton<SpawnSystem>
    {
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] towerSpawnPoints;
        [Header("Dynamic Edge Spawning")]
        [SerializeField, Tooltip("World X position to use when spawning along the right edge. If zero, uses main camera right viewport edge.")] private float rightEdgeWorldX = 0f;
        [SerializeField, Tooltip("Padding from top/bottom when picking a random Y along the right edge.")] private float verticalEdgePadding = 0.5f;
        [SerializeField, Tooltip("Check and avoid spawning overlapping other enemies when using right-edge spawn.")] private bool avoidOverlapOnRightEdge = true;
        [SerializeField, Tooltip("Radius for overlap checks when spawning along right edge.")] private float overlapCheckRadius = 0.22f;
        [Header("Auto Edge Spawn Points")] 
        [SerializeField, Tooltip("Automatically create evenly spaced spawn points along the right edge at runtime.")] private bool autoCreateRightEdgePoints = true;
        [SerializeField, Tooltip("How many right-edge spawn points to create.")] private int rightEdgePointsCount = 20;
    [SerializeField, Tooltip("Horizontal spacing used when spawning bursts along the right edge (units to the left between neighbors)." )] private float rightEdgeHorizontalSpacing = 0.75f;

        private Transform _edgePointsRoot;
        private readonly System.Collections.Generic.List<Transform> _edgePoints = new System.Collections.Generic.List<Transform>();
    private int _edgeNextIndex = 0;

        public EnemyController SpawnEnemy(EnemyData enemyData, int spawnPointId)
        {
            if (enemyData?.Prefab == null)
            {
                return null;
            }

            var point = GetSpawnPoint(enemySpawnPoints, spawnPointId);
            GameObject instance = null;

            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, point.position, Quaternion.identity);
            }

            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, point.position, Quaternion.identity);
            }

            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }

            controller.Initialize(enemyData, enemyData.PoolId);
            return controller;
        }

        public EnemyController SpawnEnemyAtRightEdge(EnemyData enemyData)
        {
            if (enemyData?.Prefab == null)
            {
                return null;
            }

            Vector3 basePos = GetRandomRightEdgePosition();
            Vector3 spawnPos = avoidOverlapOnRightEdge
                ? FindNonOverlappingRightEdgePosition(basePos)
                : basePos;
            GameObject instance = null;

            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, spawnPos, Quaternion.identity);
            }

            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, spawnPos, Quaternion.identity);
            }

            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }

            controller.Initialize(enemyData, enemyData.PoolId);
            // Force Z and renderer state to avoid flicker on first frame
            var p = instance.transform.position; instance.transform.position = new Vector3(p.x, p.y, 0f);
            var srs = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    var r = srs[i]; if (r == null) continue; var c = r.color; c.a = 1f; r.color = c; if (r.sortingOrder < 2) r.sortingOrder = 2;
                }
            }
            return controller;
        }

        public EnemyController SpawnEnemyAtRightEdge(EnemyData enemyData, float normalizedY)
        {
            if (enemyData?.Prefab == null)
            {
                return null;
            }

            Vector3 basePos = GetRightEdgePositionAtNormalizedY(Mathf.Clamp01(normalizedY));
            Vector3 spawnPos = avoidOverlapOnRightEdge
                ? FindNonOverlappingRightEdgePosition(basePos)
                : basePos;

            GameObject instance = null;
            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, spawnPos, Quaternion.identity);
            }
            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, spawnPos, Quaternion.identity);
            }
            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }
            controller.Initialize(enemyData, enemyData.PoolId);
            var p = instance.transform.position; instance.transform.position = new Vector3(p.x, p.y, 0f);
            var srs = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    var r = srs[i]; if (r == null) continue; var c = r.color; c.a = 1f; r.color = c; if (r.sortingOrder < 2) r.sortingOrder = 2;
                }
            }
            return controller;
        }

        public int EnsureEdgeSpawnPoints()
        {
            if (!autoCreateRightEdgePoints)
            {
                return _edgePoints.Count;
            }

            if (_edgePointsRoot == null)
            {
                var root = new GameObject("_AutoRightEdgeSpawnPoints");
                root.transform.SetParent(transform);
                _edgePointsRoot = root.transform;
            }

            // Adjust count
            rightEdgePointsCount = Mathf.Max(1, rightEdgePointsCount);

            // Create or resize list
            while (_edgePoints.Count < rightEdgePointsCount)
            {
                var t = new GameObject($"RightEdgePoint_{_edgePoints.Count}").transform;
                t.SetParent(_edgePointsRoot);
                _edgePoints.Add(t);
            }
            while (_edgePoints.Count > rightEdgePointsCount)
            {
                var last = _edgePoints[_edgePoints.Count - 1];
                if (last != null) Destroy(last.gameObject);
                _edgePoints.RemoveAt(_edgePoints.Count - 1);
            }

            // Position them evenly
            if (TryGetRightEdgeVerticalRange(out float minY, out float maxY, out float rightX))
            {
                for (int i = 0; i < _edgePoints.Count; i++)
                {
                    float t = (i + 0.5f) / _edgePoints.Count;
                    float y = Mathf.Lerp(minY, maxY, t);
                    var tr = _edgePoints[i];
                    if (tr != null)
                    {
                        tr.position = new Vector3(rightX, y, 0f);
                    }
                }
            }

            return _edgePoints.Count;
        }

        public int EnsureEdgeSpawnPoints(int minCount)
        {
            if (!autoCreateRightEdgePoints)
            {
                return _edgePoints.Count;
            }

            rightEdgePointsCount = Mathf.Max(rightEdgePointsCount, Mathf.Max(1, minCount));
            return EnsureEdgeSpawnPoints();
        }

        public int EdgeSpawnPointCount => _edgePoints.Count;

        public Transform GetEdgeSpawnPoint(int index)
        {
            if (_edgePoints.Count == 0) EnsureEdgeSpawnPoints();
            if (_edgePoints.Count == 0) return null;
            index = Mathf.Clamp(index, 0, _edgePoints.Count - 1);
            return _edgePoints[index];
        }

        public EnemyController SpawnEnemyAtEdgeIndex(EnemyData enemyData, int index)
        {
            EnsureEdgeSpawnPoints();
            var point = GetEdgeSpawnPoint(index);
            if (point == null)
            {
                return SpawnEnemyAtRightEdge(enemyData);
            }

            GameObject instance = null;
            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, point.position, Quaternion.identity);
            }
            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, point.position, Quaternion.identity);
            }
            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }
            controller.Initialize(enemyData, enemyData.PoolId);
            var p = instance.transform.position; instance.transform.position = new Vector3(p.x, p.y, 0f);
            var srs = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    var r = srs[i]; if (r == null) continue; var c = r.color; c.a = 1f; r.color = c; if (r.sortingOrder < 2) r.sortingOrder = 2;
                }
            }
            return controller;
        }

        public EnemyController SpawnEnemyAtEdgeNext(EnemyData enemyData)
        {
            int count = EnsureEdgeSpawnPoints();
            if (count <= 0)
            {
                return SpawnEnemyAtRightEdge(enemyData);
            }
            int idx = _edgeNextIndex % count;
            _edgeNextIndex = (idx + 1) % count;
            return SpawnEnemyAtEdgeIndex(enemyData, idx);
        }

        protected override void Awake()
        {
            base.Awake();
            // Ensure reasonable defaults if this component existed before fields were added or values are zeroed in the scene
            if (rightEdgeHorizontalSpacing <= 0f) rightEdgeHorizontalSpacing = 1.0f;
            if (autoCreateRightEdgePoints && rightEdgePointsCount < 1) rightEdgePointsCount = 20;

            // Capture baseline camera extents so spawns are not affected by runtime zoom
            _camCache = Camera.main;
            if (_camCache != null)
            {
                if (_camCache.orthographic)
                {
                    _baseHalfHeight = _camCache.orthographicSize;
                }
                else
                {
                    // For perspective, approximate baseline using initial viewport bounds
                    var bottom = _camCache.ViewportToWorldPoint(new Vector3(0f, 0f, _camCache.nearClipPlane + 1f));
                    var top = _camCache.ViewportToWorldPoint(new Vector3(0f, 1f, _camCache.nearClipPlane + 1f));
                    _baseHalfHeight = Mathf.Abs(top.y - bottom.y) * 0.5f;
                }
                _baseAspect = Mathf.Max(0.1f, _camCache.aspect);
                _baseCamPosY = _camCache.transform.position.y;
                _baseCamPosX = _camCache.transform.position.x;
                _baselineCaptured = true;
            }
        }

    public float RightEdgeHorizontalSpacing => Mathf.Max(0f, rightEdgeHorizontalSpacing);

    public int EnemyLaneSpawnPointCount => enemySpawnPoints != null ? enemySpawnPoints.Length : 0;

        public EnemyController SpawnEnemyAtPosition(EnemyData enemyData, Vector3 position)
        {
            if (enemyData?.Prefab == null)
            {
                return null;
            }

            // Optional anti-overlap nudge for explicit positions (reuses right-edge overlap settings)
            position = FindNonOverlappingRightEdgePosition(position, 4);

            GameObject instance = null;
            if (!string.IsNullOrEmpty(enemyData.PoolId) && ObjectPoolManager.HasInstance)
            {
                instance = ObjectPoolManager.Instance.Spawn(enemyData.PoolId, position, Quaternion.identity);
            }
            if (instance == null)
            {
                instance = Instantiate(enemyData.Prefab, position, Quaternion.identity);
            }
            if (!instance.TryGetComponent(out EnemyController controller))
            {
                controller = instance.AddComponent<EnemyController>();
            }
            controller.Initialize(enemyData, enemyData.PoolId);
            var p = instance.transform.position; instance.transform.position = new Vector3(p.x, p.y, 0f);
            var srs = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null)
            {
                for (int i = 0; i < srs.Length; i++)
                {
                    var r = srs[i]; if (r == null) continue; var c = r.color; c.a = 1f; r.color = c; if (r.sortingOrder < 2) r.sortingOrder = 2;
                }
            }
            return controller;
        }

        private Vector3 FindNonOverlappingRightEdgePosition(Vector3 basePos, int maxAttempts = 8)
        {
            // Try a few times to find a right-edge position not overlapping other enemies
            float sign = 1f;
            float step = Mathf.Max(0.05f, overlapCheckRadius * 1.25f);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 pos = basePos;
                if (attempt > 0)
                {
                    // Nudge up/down alternately with growing amplitude
                    float offset = step * attempt;
                    pos.y += offset * sign;
                    sign *= -1f;
                }

                bool overlapsEnemy = false;
                var hits = Physics2D.OverlapCircleAll(pos, overlapCheckRadius);
                if (hits != null)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var h = hits[i];
                        if (h == null) continue;
                        if (h.GetComponent<EnemyController>() != null)
                        {
                            overlapsEnemy = true;
                            break;
                        }
                    }
                }

                if (!overlapsEnemy)
                {
                    return new Vector3(pos.x, pos.y, 0f);
                }
            }

            // Fallback to a random position if all attempts overlap
            return GetRandomRightEdgePosition();
        }

        private Vector3 GetRandomRightEdgePosition()
        {
            if (!TryGetRightEdgeVerticalRange(out float minY, out float maxY, out float rightX))
            {
                return Vector3.zero;
            }
            float y = Random.Range(minY, maxY);
            return new Vector3(rightX, y, 0f);
        }

        public Vector3 GetRightEdgePositionAtNormalizedY(float t)
        {
            if (!TryGetRightEdgeVerticalRange(out float minY, out float maxY, out float rightX))
            {
                return Vector3.zero;
            }
            float y = Mathf.Lerp(minY, maxY, Mathf.Clamp01(t));
            return new Vector3(rightX, y, 0f);
        }

        public bool TryGetRightEdgeVerticalRange(out float minY, out float maxY)
        {
            return TryGetRightEdgeVerticalRange(out minY, out maxY, out _);
        }

        // Cache baseline camera extents at startup so spawns are not affected by runtime zoom
        private bool _baselineCaptured = false;
        private float _baseHalfHeight;
        private float _baseAspect;
        private float _baseCamPosY;
        private float _baseCamPosX;
        private Camera _camCache;

        

        private bool TryGetRightEdgeVerticalRange(out float minY, out float maxY, out float rightX)
        {
            var cam = _camCache != null ? _camCache : Camera.main;
            if (cam == null)
            {
                // Fallback when no MainCamera is present: assume a sensible range around this system
                float cy = transform.position.y;
                minY = cy - 5f + verticalEdgePadding;
                maxY = cy + 5f - verticalEdgePadding;
                rightX = (rightEdgeWorldX != 0f) ? rightEdgeWorldX : transform.position.x + 10f;
                Debug.LogWarning("[SpawnSystem] Camera.main not found. Using fallback vertical range around SpawnSystem. rightX=" + rightX.ToString("F2"));
                if (maxY < minY)
                {
                    (minY, maxY) = (maxY, minY);
                }
                // Ensure minimum span so vertical distribution never collapses
                float minSpan = 1.0f;
                float span = maxY - minY;
                if (span < minSpan)
                {
                    float cy2 = (minY + maxY) * 0.5f;
                    minY = cy2 - minSpan * 0.5f;
                    maxY = cy2 + minSpan * 0.5f;
                }
                return true;
            }

            if (rightEdgeWorldX != 0f)
            {
                rightX = rightEdgeWorldX;
            }
            else if (cam.orthographic)
            {
                // Use baseline extents if captured to avoid zoom-in/out affecting spawn positions
                if (_baselineCaptured)
                {
                    float halfH = _baseHalfHeight;
                    float halfW = halfH * _baseAspect;
                    rightX = _baseCamPosX + halfW;
                }
                else
                {
                    float halfH = cam.orthographicSize;
                    float halfW = halfH * cam.aspect;
                    rightX = cam.transform.position.x + halfW;
                }
            }
            else
            {
                var right = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, cam.nearClipPlane + 1f));
                rightX = right.x;
            }

            if (cam.orthographic)
            {
                if (_baselineCaptured)
                {
                    float halfH = _baseHalfHeight;
                    float cy = _baseCamPosY;
                    minY = cy - halfH + verticalEdgePadding;
                    maxY = cy + halfH - verticalEdgePadding;
                }
                else
                {
                    float halfH = cam.orthographicSize;
                    float cy = cam.transform.position.y;
                    minY = cy - halfH + verticalEdgePadding;
                    maxY = cy + halfH - verticalEdgePadding;
                }
            }
            else
            {
                var bottom = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane + 1f));
                var top = cam.ViewportToWorldPoint(new Vector3(0f, 1f, cam.nearClipPlane + 1f));
                minY = Mathf.Min(bottom.y, top.y) + verticalEdgePadding;
                maxY = Mathf.Max(bottom.y, top.y) - verticalEdgePadding;
            }

            if (maxY < minY)
            {
                (minY, maxY) = (maxY, minY);
            }
            // Enforce a minimum vertical span so distribution never collapses
            float minSpanOrtho = 1.0f;
            float span2 = maxY - minY;
            if (span2 < minSpanOrtho)
            {
                float cy3 = (minY + maxY) * 0.5f;
                minY = cy3 - minSpanOrtho * 0.5f;
                maxY = cy3 + minSpanOrtho * 0.5f;
            }
            return true;
        }

        public TowerBehaviour SpawnTower(TowerData towerData, Vector3 position, Transform parent = null)
        {
            if (towerData?.Prefab == null)
            {
                return null;
            }

            var instance = Instantiate(towerData.Prefab, position, Quaternion.identity, parent);
            var behaviour = instance.GetComponent<TowerBehaviour>();
            behaviour.Initialize(towerData);
            // Ensure a 2D collider exists; add a BoxCollider2D sized to sprite bounds if missing
            if (instance.GetComponent<Collider2D>() == null)
            {
                var box = instance.AddComponent<BoxCollider2D>();
                // Make towers solid so enemies cannot walk through them
                box.isTrigger = false;
                // Compute bounds from child SpriteRenderers
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    // Convert world bounds size to local by inverse scale
                    var lossy = instance.transform.lossyScale;
                    float sx = Mathf.Approximately(lossy.x, 0f) ? 1f : lossy.x;
                    float sy = Mathf.Approximately(lossy.y, 0f) ? 1f : lossy.y;
                    box.size = new Vector2(bounds.size.x / sx, bounds.size.y / sy);
                    box.offset = instance.transform.InverseTransformPoint(bounds.center) - (Vector3)instance.transform.InverseTransformPoint(instance.transform.position);
                }
            }
            return behaviour;
        }

        private Transform GetSpawnPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0)
            {
                var fallback = new GameObject("SpawnPoint").transform;
                fallback.position = Vector3.zero;
                return fallback;
            }

            index = Mathf.Clamp(index, 0, points.Length - 1);
            return points[index];
        }
    }
}
