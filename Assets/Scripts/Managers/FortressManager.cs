using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Fortress
{
    public class FortressManager : Singleton<FortressManager>
    {
        [SerializeField] private FortressConfig config;
        [Header("Core Settings")]
        [SerializeField, Range(0.1f, 1f)] private float coreVisualScale = 0.35f; // make core look smaller while keeping barrier full-size

        private readonly List<FortressWall> _walls = new();
        private readonly List<FortressMount> _mounts = new();
        private bool _built;
    private int _rows;
    private int _cols;
    private int _coreRow;
    private int _coreCol;

        public FortressConfig Config => config;
        public IReadOnlyList<FortressMount> Mounts => _mounts;

        private void Start()
        {
            if (config != null)
            {
                Debug.Log("FortressManager: Building fortress using FortressConfig.", this);
            }
            else
            {
                Debug.Log("FortressManager: No FortressConfig assigned, using fallback defaults.", this);
            }
            BuildFortress();
        }

        private void BuildFortress()
        {
            if (_built)
            {
                return;
            }

            ClearExisting();

            int rows;
            int cols;
            Vector2 spacing;
            Vector3 origin;
            int coreRow;
            int coreCol;
            GameObject corePrefab = null;
            FortressWall wallPrefab = null;

            if (config != null)
            {
                rows = config.Rows;
                cols = config.Columns;
                spacing = config.CellSpacing;
                origin = transform.position + (Vector3)config.OriginOffset;
                coreRow = config.CoreRow;
                coreCol = config.CoreColumn;
                corePrefab = config.CorePrefab;
                wallPrefab = config.WallPrefab;
            }
            else
            {
                // Fallback defaults if no config is assigned
                rows = 3;
                cols = 2;
                // Unit-sized cells for tight tiling
                float cell = 1.0f;
                spacing = new Vector2(cell, cell);
                // Anchor to the left side of the viewport and center vertically
                var cam = Camera.main;
                if (cam != null)
                {
                    float depth = cam.orthographic ? 0f : Mathf.Abs(cam.transform.position.z);
                    var leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth));
                    var mid = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                    // origin is bottom-left cell center; align left column to touch the viewport edge
                    float totalHeight = (rows - 1) * spacing.y;
                    float originX = leftEdge.x + (spacing.x * 0.5f);
                    float originY = mid.y - (totalHeight * 0.5f);
                    origin = new Vector3(originX, originY, 0f);
                }
                else
                {
                    origin = transform.position;
                }
                // Core at [row 1, col 0]
                coreRow = 1;
                coreCol = 0;

                // Try to find prefabs in known path
                corePrefab = Resources.Load<GameObject>("Prefabs/Fortress/FortressCore");
                if (corePrefab == null)
                {
                    // As a last resort, instantiate an empty object as core marker
                    corePrefab = new GameObject("FortressCoreFallback");
                }

                var wallPrefabObj = Resources.Load<GameObject>("Prefabs/Fortress/FortressWall");
                if (wallPrefabObj != null)
                {
                    wallPrefab = wallPrefabObj.GetComponent<FortressWall>();
                }
            }

            _rows = rows;
            _cols = cols;
            _coreRow = coreRow;
            _coreCol = coreCol;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Vector3 worldPosition = origin + new Vector3(col * spacing.x, (rows - 1 - row) * spacing.y, 0f);
                    bool isCore = row == coreRow && col == coreCol;

                    if (isCore)
                    {
                        SpawnCoreWith(corePrefab, worldPosition);
                    }
                    else
                    {
                        SpawnWallWith(wallPrefab, row, col, worldPosition);
                    }
                }
            }

            _built = true;
            Debug.Log($"Fortress built: walls={_walls.Count}, mounts={_mounts.Count}", this);

            EnsureWallsHUD();
        }

        private void ClearExisting()
        {
            _walls.Clear();
            _mounts.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void SpawnCoreWith(GameObject prefab, Vector3 position)
        {
            GameObject coreGo;
            if (prefab != null)
            {
                coreGo = Instantiate(prefab, position, Quaternion.identity, transform);
            }
            else
            {
                // Code-based fallback core (visible)
                coreGo = new GameObject("FortressCore");
                coreGo.transform.SetParent(transform);
                coreGo.transform.position = position;
                // Visual child so we can scale visuals without affecting the barrier collider on root
                var visual = new GameObject("CoreVisual");
                visual.transform.SetParent(coreGo.transform, false);
                var r = visual.AddComponent<SpriteRenderer>();
                r.sprite = CreateCircleSprite(new Color(0.85f, 0.75f, 0.2f, 1f));
                r.sortingOrder = 1; // render above walls (0)
            }

            // Enforce Z=0 for 2D sorting
            var p = coreGo.transform.position;
            coreGo.transform.position = new Vector3(p.x, p.y, 0f);

            // Ensure there is at least one visible renderer and it's above walls. Also move or duplicate renderers under a visual root we can scale.
            var visualRoot = coreGo.transform.Find("CoreVisual");
            if (visualRoot == null)
            {
                var go = new GameObject("CoreVisual");
                visualRoot = go.transform;
                visualRoot.SetParent(coreGo.transform, false);
            }

            var renderers = coreGo.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                var r = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                r.sprite = CreateCircleSprite(new Color(0.85f, 0.75f, 0.2f, 1f));
                r.sortingOrder = 1;
                r.enabled = true;
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    // If renderer lives on a GO that also has a Collider2D, we duplicate the renderer to the visual root and disable original
                    bool hasCollider = r.GetComponent<Collider2D>() != null;
                    if (hasCollider)
                    {
                        var dup = new GameObject(r.gameObject.name + "_Visual");
                        dup.transform.SetParent(visualRoot, false);
                        dup.transform.position = r.transform.position;
                        dup.transform.rotation = r.transform.rotation;
                        var nr = dup.AddComponent<SpriteRenderer>();
                        nr.sprite = r.sprite;
                        nr.color = r.color;
                        nr.flipX = r.flipX;
                        nr.flipY = r.flipY;
                        nr.sortingLayerID = r.sortingLayerID;
                        nr.sortingOrder = Mathf.Max(1, r.sortingOrder);
                        nr.enabled = true;
                        r.enabled = false; // keep collider in place, visuals moved to duplicate
                    }
                    else
                    {
                        r.transform.SetParent(visualRoot, true);
                        r.enabled = true;
                        if (r.sortingOrder < 1)
                        {
                            r.sortingOrder = 1;
                        }
                    }
                }
            }

            // Scale only visuals, keep barrier collider full-size on core root
            visualRoot.localScale = new Vector3(coreVisualScale, coreVisualScale, 1f);

            // Ensure core has a small solid collider on the root (visual target), walls remain the barrier
            if (coreGo.GetComponent<Collider2D>() == null)
            {
                var coreCollider = coreGo.AddComponent<CircleCollider2D>();
                coreCollider.isTrigger = false;
                coreCollider.radius = 0.2f; // small circle in the middle
            }

            // Ensure BaseCore behaviour exists
            if (coreGo.GetComponent<BaseCore>() == null)
            {
                coreGo.AddComponent<BaseCore>();
            }

            // Ensure CoreHealthBar exists
            if (coreGo.GetComponent<BulletHeavenFortressDefense.UI.CoreHealthBar>() == null)
            {
                coreGo.AddComponent<BulletHeavenFortressDefense.UI.CoreHealthBar>();
            }
        }

        private void SpawnWallWith(FortressWall prefab, int row, int column, Vector3 position)
        {
            FortressWall wallInstance = null;
            if (prefab != null)
            {
                wallInstance = Instantiate(prefab, position, Quaternion.identity, transform);
            }
            else
            {
                // Code-based fallback wall with mount
                var wallGo = new GameObject($"FortressWall_{row}_{column}");
                wallGo.transform.SetParent(transform);
                wallGo.transform.position = position;

                var renderer = wallGo.AddComponent<SpriteRenderer>();
                renderer.sprite = CreateSolidSprite(new Color(0.40f, 0.50f, 0.70f, 1f));
                renderer.sortingOrder = 0;

                wallInstance = wallGo.AddComponent<FortressWall>();

                // Add a non-trigger collider to act as barrier (impenetrable wall)
                var box = wallGo.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                box.size = new Vector2(1.0f, 1.0f);

                // Create 4 mounts around center
                CreateMount(wallGo.transform, new Vector2(-0.25f, 0.25f)); // top-left
                CreateMount(wallGo.transform, new Vector2(0.25f, 0.25f));  // top-right
                CreateMount(wallGo.transform, new Vector2(-0.25f, -0.25f)); // bottom-left
                CreateMount(wallGo.transform, new Vector2(0.25f, -0.25f));  // bottom-right

                // Visual segmentation (4 faint quads + 1 center)
                CreateSegment(wallGo.transform, new Vector2(-0.25f, 0.25f));
                CreateSegment(wallGo.transform, new Vector2(0.25f, 0.25f));
                CreateSegment(wallGo.transform, new Vector2(-0.25f, -0.25f));
                CreateSegment(wallGo.transform, new Vector2(0.25f, -0.25f));
                CreateSegment(wallGo.transform, Vector2.zero);

                // Add wall damage overlay UI
                wallGo.AddComponent<BulletHeavenFortressDefense.UI.WallDamageOverlay>();
            }

            wallInstance.Initialize(this, row, column);
            if (!_walls.Contains(wallInstance))
            {
                _walls.Add(wallInstance);
            }

            if (wallInstance.Mount != null && !_mounts.Contains(wallInstance.Mount))
            {
                _mounts.Add(wallInstance.Mount);
            }
        }

        public bool TryGetMountAt(Vector2 worldPosition, out FortressMount mount)
        {
            for (int i = 0; i < _mounts.Count; i++)
            {
                var candidate = _mounts[i];
                if (candidate != null && candidate.ContainsPoint(worldPosition))
                {
                    mount = candidate;
                    return true;
                }
            }

            mount = null;
            return false;
        }

        internal void RegisterMount(FortressMount mount)
        {
            if (mount != null && !_mounts.Contains(mount))
            {
                _mounts.Add(mount);
            }
        }

        internal void UnregisterMount(FortressMount mount)
        {
            if (mount != null)
            {
                _mounts.Remove(mount);
            }
        }

        internal void NotifyWallDestroyed(FortressWall wall)
        {
            if (wall != null && _mounts.Contains(wall.Mount))
            {
                // Wall keeps the mount reference, but mount disables placement until repaired.
            }
        }

        [ContextMenu("Rebuild Fortress")]
        private void ContextRebuild()
        {
            _built = false;
            BuildFortress();
        }

        private Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private Sprite CreateCircleSprite(Color color, int size = 32)
        {
            size = Mathf.Clamp(size, 8, 256);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = (size - 1) * 0.5f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    if (d <= r)
                    {
                        tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void CreateMount(Transform parent, Vector2 localPos)
        {
            var mountGo = new GameObject("Mount");
            mountGo.transform.SetParent(parent);
            mountGo.transform.localPosition = localPos;
            var box = mountGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            var vis = mountGo.AddComponent<Fortress.MountSpotVisual>();
            // Collider remains at full quadrant size for generous click target
            box.size = new Vector2(0.5f, 0.5f);
            mountGo.AddComponent<Fortress.FortressMount>();
        }

        private void CreateSegment(Transform parent, Vector2 localPos)
        {
            var seg = new GameObject("Segment");
            seg.transform.SetParent(parent);
            seg.transform.localPosition = localPos;
            seg.transform.localScale = Vector3.one * 0.45f;
            var sr = seg.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSolidSprite(new Color(1f, 1f, 1f, 0.08f));
            sr.sortingOrder = -1; // under mounts/towers
        }

        public IReadOnlyList<FortressWall> GetActiveWalls()
        {
            return _walls;
        }

        public int Rows => _rows;
        public int Columns => _cols;
        public int CoreRow => _coreRow;
        public int CoreColumn => _coreCol;

        private void EnsureWallsHUD()
        {
            if (FindObjectOfType<BulletHeavenFortressDefense.UI.WallsHealthHUD>() != null)
            {
                return;
            }

            var hudRoot = new GameObject("WallsHealthHUD");
            hudRoot.transform.SetParent(null); // top-level UI object
            hudRoot.AddComponent<BulletHeavenFortressDefense.UI.WallsHealthHUD>();
        }
    }
}
