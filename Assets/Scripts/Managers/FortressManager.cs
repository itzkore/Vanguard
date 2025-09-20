using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Entities;
using UnityEngine.UI; // needed for Canvas/Button/Image/Text/GraphicRaycaster/CanvasScaler

namespace BulletHeavenFortressDefense.Fortress
{
    public class FortressManager : Singleton<FortressManager>
    {
        [SerializeField] private FortressConfig config;
        [Header("Core Settings")]
        [SerializeField, Range(0.1f, 1f)] private float coreVisualScale = 0.35f;
        [Header("Layout Scaling")]
        [Tooltip("Uniform scale applied to fallback fortress cells (walls & mounts). 1 = previous size.")]
        [SerializeField, Range(0.5f, 4f)] private float wallScale = 1.0f;
        [Header("Auto Alignment")]
        [SerializeField, Tooltip("If true, fortress will always be horizontally aligned flush to the left camera edge (with optional padding) regardless of resolution/aspect.")] private bool autoAlignLeft = true;
        [SerializeField, Tooltip("Extra world-unit padding from the absolute left edge of the camera when autoAlignLeft is on.")] private float leftPadding = 0f;

        private readonly List<FortressWall> _walls = new();
        private readonly List<FortressMount> _mounts = new();
        private bool _built;
    private int _rows;
    private int _cols;
    private int _coreRow;
    private int _coreCol;
    private Vector2 _cellSpacing; // cached spacing used when building
    private int _lastScreenW = -1;
    private int _lastScreenH = -1;
    // Breach tracking: enemies can head for core if either the middle wall (to the right of the core) is destroyed,
    // or at least two walls in total are destroyed.
    private int _destroyedWallsCount;
    private bool _middleWallDestroyed;

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

        private void Update()
        {
            if (!autoAlignLeft || !_built) return;
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                AlignLeft();
            }
        }

        private void BuildFortress()
        {
            if (_built)
            {
                return;
            }

            ClearExisting();

            // Reset breach tracking when (re)building
            _destroyedWallsCount = 0;
            _middleWallDestroyed = false;

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
                float baseCell = 1.0f * wallScale; // scaled cell size
                spacing = new Vector2(baseCell, baseCell);
                var cam = Camera.main;
                if (cam != null)
                {
                    float depth = cam.orthographic ? 0f : Mathf.Abs(cam.transform.position.z);
                    var leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth));
                    var mid = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                    float totalHeight = (rows - 1) * spacing.y;
                    float originX = leftEdge.x + (spacing.x * 0.5f); // keep left column flush
                    float originY = mid.y - (totalHeight * 0.5f);
                    origin = new Vector3(originX, originY, 0f);
                }
                else { origin = transform.position; }
                coreRow = 1; coreCol = 0;
                corePrefab = Resources.Load<GameObject>("Prefabs/Fortress/FortressCore");
                if (corePrefab == null) corePrefab = new GameObject("FortressCoreFallback");
                var wallPrefabObj = Resources.Load<GameObject>("Prefabs/Fortress/FortressWall");
                if (wallPrefabObj != null) wallPrefab = wallPrefabObj.GetComponent<FortressWall>();
            }

            _rows = rows;
            _cols = cols;
            _coreRow = coreRow;
            _coreCol = coreCol;
            _cellSpacing = spacing;

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

            if (autoAlignLeft)
            {
                AlignLeft();
            }

            EnsureWallsHUD();
        }

        private void ClearExisting()
        {
            // Reset breach tracking
            _destroyedWallsCount = 0;
            _middleWallDestroyed = false;

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
                wallGo.transform.localScale = Vector3.one * wallScale; // apply scale to whole wall cluster

                var renderer = wallGo.AddComponent<SpriteRenderer>();
                // Procedural dark gray brick texture instead of flat beige
                renderer.sprite = CreateBrickSprite();
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
            if (wall == null) return;
            // Count unique destroyed walls by scanning, but keep a fast counter too
            _destroyedWallsCount = Mathf.Max(_destroyedWallsCount + 1, 0);

            // If this is the wall immediately to the right of the core row/col, mark as middle breach
            if (wall.Row == _coreRow && wall.Column == _coreCol + 1)
            {
                _middleWallDestroyed = true;
            }
            // Wall keeps the mount reference, but mount disables placement until repaired.
        }

        internal void NotifyWallRepaired(FortressWall wall)
        {
            if (wall == null) return;
            _destroyedWallsCount = Mathf.Max(0, _destroyedWallsCount - 1);
            if (wall.Row == _coreRow && wall.Column == _coreCol + 1)
            {
                // Middle wall was repaired; mark false unless some other logic re-sets it later
                _middleWallDestroyed = false;
            }
            // As a safety net, recompute flags in case of mismatch
            RecomputeBreachState();
        }

        private void RecomputeBreachState()
        {
            int destroyed = 0;
            bool middle = false;
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (w == null) continue;
                if (w.IsDestroyed)
                {
                    destroyed++;
                    if (w.Row == _coreRow && w.Column == _coreCol + 1)
                    {
                        middle = true;
                    }
                }
            }
            _destroyedWallsCount = destroyed;
            _middleWallDestroyed = middle;
        }

        public int DestroyedWallsCount => _destroyedWallsCount;
        public bool MiddleWallDestroyed => _middleWallDestroyed;
        public bool IsCoreBreachable => _middleWallDestroyed || _destroyedWallsCount >= 2;

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
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        private Sprite CreateBrickSprite()
        {
            // 32x32 texture with 8x4 brick layout (each brick 4x8 px) dark gray mortar lines
            int w = 64; int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color brick = new Color(0.22f, 0.22f, 0.24f, 1f); // primary brick
            Color brick2 = new Color(0.26f, 0.26f, 0.28f, 1f); // subtle variation
            Color mortar = new Color(0.08f, 0.08f, 0.09f, 1f);
            int rows = 8; int cols = 8; // bricks grid
            int brickH = h / rows; int brickW = w / cols;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool mortarLine = (y % brickH == 0) || (x % brickW == 0);
                    if (mortarLine)
                    {
                        tex.SetPixel(x, y, mortar);
                    }
                    else
                    {
                        int cy = y / brickH; int cx = x / brickW;
                        bool alt = (cx + cy) % 2 == 0;
                        tex.SetPixel(x, y, alt ? brick : brick2);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0.5f,0.5f), 64f, 0, SpriteMeshType.FullRect);
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private void CreateMount(Transform parent, Vector2 localPos)
        {
            var mountGo = new GameObject("Mount");
            mountGo.transform.SetParent(parent);
            mountGo.transform.localPosition = localPos * wallScale; // keep relative spacing proportional
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
            seg.transform.localPosition = localPos * wallScale;
            seg.transform.localScale = Vector3.one * 0.45f * wallScale;
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

        [Header("UI Options")] 
        [SerializeField, Tooltip("If false, the top-left 'Menu' button (pause toggle) will NOT be created.")] private bool createTopLeftMenuButton = false;

        private void EnsureWallsHUD()
        {
            if (FindObjectOfType<BulletHeavenFortressDefense.UI.WallsHealthHUD>() != null)
            {
                return;
            }

            var hudRoot = new GameObject("WallsHealthHUD");
            hudRoot.transform.SetParent(null); // top-level UI object
            hudRoot.AddComponent<BulletHeavenFortressDefense.UI.WallsHealthHUD>();
            if (createTopLeftMenuButton)
            {
                CreateAdjacentMenuButton();
            }
        }

        private void CreateAdjacentMenuButton()
        {
            if (GameObject.Find("TopLeftMenuButtonCanvas") != null) return;
            var canvasGO = new GameObject("TopLeftMenuButtonCanvas", typeof(RectTransform));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 610;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGO.AddComponent<GraphicRaycaster>();
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f,1f); rt.anchorMax = new Vector2(0f,1f); rt.pivot = new Vector2(0f,1f);
            rt.anchoredPosition = new Vector2(170f, -8f);

            var btnGO = new GameObject("MenuButton", typeof(RectTransform));
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.SetParent(canvasGO.transform,false);
            btnRT.sizeDelta = new Vector2(112f,40f);
            btnRT.anchorMin = new Vector2(0f,1f); btnRT.anchorMax = new Vector2(0f,1f); btnRT.pivot = new Vector2(0f,1f);
            btnRT.anchoredPosition = Vector2.zero;
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.12f,0.14f,0.18f,0.95f);
            var btn = btnGO.AddComponent<Button>();
            var cols = btn.colors; cols.highlightedColor = new Color(0.22f,0.26f,0.32f,1f); cols.pressedColor = new Color(0.05f,0.06f,0.08f,1f); btn.colors = cols;
            var textGO = new GameObject("Label", typeof(RectTransform));
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.SetParent(btnRT,false); textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one; textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
            var txt = textGO.AddComponent<Text>();
            txt.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
            txt.text = "Menu"; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white; txt.fontSize = 20;

            var pause = Object.FindObjectOfType<BulletHeavenFortressDefense.UI.PauseMenu>();
            if (pause == null)
            {
                var pmGO = new GameObject("PauseMenu");
                pause = pmGO.AddComponent<BulletHeavenFortressDefense.UI.PauseMenu>();
            }
            btn.onClick.AddListener(() => { if (Time.timeScale == 0f) pause.Resume(); else pause.Pause(); });
        }

        private void AlignLeft()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float depth = cam.orthographic ? 0f : Mathf.Abs(cam.transform.position.z - transform.position.z);
            float camLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x + leftPadding;

            // Find current left-most wall (or core) center X (column 0)
            float minX = float.PositiveInfinity;
            for (int i = 0; i < _walls.Count; i++)
            {
                var w = _walls[i];
                if (w == null) continue;
                if (w.Column == 0)
                {
                    float x = w.transform.position.x;
                    if (x < minX) minX = x;
                }
            }
            // Include core if it sits in column 0
            if (_coreCol == 0)
            {
                // try to locate core via name or BaseCore component
                var core = GetComponentInChildren<BaseCore>();
                if (core != null)
                {
                    float cx = core.transform.position.x;
                    if (cx < minX) minX = cx;
                }
            }
            if (float.IsInfinity(minX)) return; // nothing built yet

            // Desired center of column 0 cell should be camLeft + half cell width (spacing.x * 0.5)
            float halfCell = _cellSpacing.x * 0.5f;
            float desiredCol0Center = camLeft + halfCell;
            float delta = desiredCol0Center - minX;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                transform.position += new Vector3(delta, 0f, 0f);
            }
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
        }
    }
}
