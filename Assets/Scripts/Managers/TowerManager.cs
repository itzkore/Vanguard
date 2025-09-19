using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BulletHeavenFortressDefense.Managers
{
    public class TowerManager : Singleton<TowerManager>
    {
        [SerializeField] private List<TowerData> unlockedTowers = new List<TowerData>();
        [SerializeField, Tooltip("Optional extra towers appended at runtime (use if you don't want to move assets into Resources)." )]
        private List<TowerData> additionalTowers = new List<TowerData>();
        [SerializeField] private LayerMask placementMask;
        [SerializeField] private float placementRadius = 0.45f;
        [Header("Rendering")]
        [SerializeField] private int towerSortingOrder = 5; // ensure towers render above walls (order 0)

        private readonly List<Entities.TowerBehaviour> _activeTowers = new();

        public IReadOnlyList<TowerData> UnlockedTowers => unlockedTowers;
    // Expose active towers for selection / diagnostics (read-only)
    public IReadOnlyList<Entities.TowerBehaviour> ActiveTowers => _activeTowers;

        protected override void Awake()
        {
            // Important: ensure Singleton<T> sets the static instance
            base.Awake();

            // Ensure there is at least one tower to select during development
            SeedDefaultsIfEmpty();
            // Always attempt to discover any newly added TowerData assets (e.g., sniper) that were not present earlier
            DiscoverAdditionalTowers();
            // Merge explicitly assigned additional towers
            MergeAdditionalTowers();
            // Remove any accidental duplicates (same asset reference) then optionally sort
            DeduplicateAndSort();
            Debug.Log($"[TowerManager] Awake. Unlocked towers: {unlockedTowers?.Count ?? 0}");
            if (unlockedTowers != null)
            {
                for (int i = 0; i < unlockedTowers.Count; i++)
                {
                    var t = unlockedTowers[i];
                    Debug.Log($"[TowerManager] Unlocked: {(t != null ? t.name : "<null>")}");
                }
            }
        }

    // Editor-only population utility must be excluded from player builds (calls AssetDatabase)
#if UNITY_EDITOR
    [ContextMenu("Populate Unlocked Towers (Editor)")]
    private void EditorPopulateUnlocked()
    {
        unlockedTowers ??= new List<TowerData>();
        unlockedTowers.Clear();
        LoadAllTowerDataInto(unlockedTowers);
    }
#endif

        private void SeedDefaultsIfEmpty()
        {
            if (unlockedTowers != null && unlockedTowers.Count > 0)
            {
                return; // list already has entries; discovery will still run afterwards
            }
            unlockedTowers ??= new List<TowerData>();
#if UNITY_EDITOR
            LoadAllTowerDataInto(unlockedTowers);
            Debug.Log($"[TowerManager] Seeded from assets (editor). Count={unlockedTowers.Count}");
#else
            // Runtime (build) fallback: attempt to load from Resources folder if present.
            // Place TowerData assets under Assets/Resources/TowerData/ for auto-inclusion.
            var loaded = Resources.LoadAll<TowerData>("TowerData");
            if (loaded != null && loaded.Length > 0)
            {
                for (int i = 0; i < loaded.Length; i++)
                {
                    var td = loaded[i];
                    if (td == null || unlockedTowers.Contains(td)) continue;
                    unlockedTowers.Add(td);
                }
                Debug.Log($"[TowerManager] Runtime Resources fallback loaded {loaded.Length} TowerData assets. List count={unlockedTowers.Count}");
            }
            else
            {
                Debug.LogWarning("[TowerManager] No unlocked towers and Resources/TowerData empty. Add TowerData assets there or assign in inspector.");
            }
#endif
        }

        /// <summary>
        /// Always run (in editor & runtime). Attempts to find any TowerData assets not already in unlockedTowers.
        /// In builds we rely on Resources/TowerData. In editor we can also use AssetDatabase for convenience.
        /// </summary>
        private void DiscoverAdditionalTowers()
        {
            int before = unlockedTowers.Count;
#if UNITY_EDITOR
            // Editor: full asset search
            var temp = new List<TowerData>();
            LoadAllTowerDataInto(temp);
            for (int i = 0; i < temp.Count; i++)
            {
                var td = temp[i]; if (td == null) continue; if (unlockedTowers.Contains(td)) continue; unlockedTowers.Add(td);
            }
#else
            // Runtime: attempt additional Resources load (covers case where list had partial manual assignments)
            var fromResources = Resources.LoadAll<TowerData>("TowerData");
            if (fromResources != null)
            {
                for (int i = 0; i < fromResources.Length; i++)
                {
                    var td = fromResources[i]; if (td == null) continue; if (unlockedTowers.Contains(td)) continue; unlockedTowers.Add(td);
                }
            }
#endif
            int added = unlockedTowers.Count - before;
            if (added > 0)
            {
                Debug.Log($"[TowerManager] DiscoverAdditionalTowers added {added} new tower(s). Total={unlockedTowers.Count}");
            }
        }

        private void MergeAdditionalTowers()
        {
            if (additionalTowers == null || additionalTowers.Count == 0) return;
            int before = unlockedTowers.Count;
            for (int i = 0; i < additionalTowers.Count; i++)
            {
                var td = additionalTowers[i];
                if (td == null || unlockedTowers.Contains(td)) continue;
                unlockedTowers.Add(td);
            }
            int added = unlockedTowers.Count - before;
            if (added > 0)
            {
                Debug.Log($"[TowerManager] MergeAdditionalTowers added {added} tower(s) from explicit list.");
            }
        }

        private void DeduplicateAndSort()
        {
            if (unlockedTowers == null || unlockedTowers.Count == 0) return;
            var seen = new HashSet<TowerData>();
            bool removedAny = false;
            for (int i = unlockedTowers.Count - 1; i >= 0; i--)
            {
                var td = unlockedTowers[i];
                if (td == null) { unlockedTowers.RemoveAt(i); removedAny = true; continue; }
                if (!seen.Add(td)) { unlockedTowers.RemoveAt(i); removedAny = true; }
            }
            if (removedAny)
            {
                Debug.Log("[TowerManager] Removed duplicate tower references after discovery/merge.");
            }
            // Stable sort by build cost then name for consistent UI ordering
            unlockedTowers.Sort((a,b) => {
                if (a == b) return 0;
                if (a == null) return 1; if (b == null) return -1;
                int costCmp = a.BuildCost.CompareTo(b.BuildCost);
                if (costCmp != 0) return costCmp;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });
        }

#if UNITY_EDITOR
        [ContextMenu("Force Discover Towers (All)")]
        private void EditorForceDiscover()
        {
            if (unlockedTowers == null) unlockedTowers = new List<TowerData>();
            DiscoverAdditionalTowers();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[TowerManager] Forced discovery executed via context menu.");
        }
#endif

#if UNITY_EDITOR
        private static void LoadAllTowerDataInto(List<TowerData> target)
        {
            var guids = AssetDatabase.FindAssets("t:TowerData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TowerData>(path);
                if (asset != null && !target.Contains(asset))
                {
                    target.Add(asset);
                    Debug.Log($"[TowerManager] Found TowerData: {asset.name} at {path}");
                }
            }

            // Fallback: search specific Towers folder if generic type search found nothing
            if (target.Count == 0)
            {
                var folder = new[] { "Assets/ScriptableObjects/Towers" };
                var guids2 = AssetDatabase.FindAssets("t:ScriptableObject", folder);
                foreach (var guid2 in guids2)
                {
                    var path2 = AssetDatabase.GUIDToAssetPath(guid2);
                    var asset2 = AssetDatabase.LoadAssetAtPath<TowerData>(path2);
                    if (asset2 != null && !target.Contains(asset2))
                    {
                        target.Add(asset2);
                        Debug.Log($"[TowerManager] Fallback found TowerData: {asset2.name} at {path2}");
                    }
                }
            }
        }
#endif

        public bool TryPlaceTower(TowerData towerData, Vector3 targetPosition)
        {
            if (towerData == null)
            {
                return false;
            }

            if (!CanPlace(targetPosition))
            {
                return false;
            }

            var tower = Systems.SpawnSystem.Instance.SpawnTower(towerData, targetPosition);
            if (tower != null)
            {
                ConfigureTowerRendering(tower);
                _activeTowers.Add(tower);
                return true;
            }

            return false;
        }

        public bool TryPlaceTowerOnMount(TowerData towerData, FortressMount mount)
        {
            if (towerData == null || mount == null || !mount.CanPlaceTower())
            {
                return false;
            }

            var tower = Systems.SpawnSystem.Instance.SpawnTower(towerData, mount.GetPlacementPosition(), mount.transform);
            if (tower == null)
            {
                return false;
            }

            tower.transform.localPosition = Vector3.zero;
            // Scale tower to fit the mount square exactly using sprite bounds
            FitTowerToMount(tower, mount.GetSpotSize());
            ConfigureTowerRendering(tower);
            mount.AttachTower(tower);
            // Track invested energy for selling
            var levelField = typeof(Entities.TowerBehaviour).GetField("level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var investedField = typeof(Entities.TowerBehaviour).GetField("investedEnergy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField?.SetValue(tower, 1);
            int buildInvest = towerData.BuildCost;
            if (Systems.EconomySystem.HasInstance)
            {
                buildInvest = Systems.EconomySystem.Instance.GetScaledBuildCost(towerData);
            }
            investedField?.SetValue(tower, buildInvest);
            _activeTowers.Add(tower);
            return true;
        }

        public void RemoveTower(Entities.TowerBehaviour tower)
        {
            if (tower == null)
            {
                return;
            }

            _activeTowers.Remove(tower);
            if (tower.gameObject != null)
            {
                Object.Destroy(tower.gameObject);
            }
        }

        private bool CanPlace(Vector3 position)
        {
            return Physics2D.OverlapCircle(position, placementRadius, placementMask) == null;
        }

        private void ConfigureTowerRendering(Entities.TowerBehaviour tower)
        {
            if (tower == null) return;

            // Ensure Z = 0 so sorting by order is consistent in 2D
            var pos = tower.transform.position;
            tower.transform.position = new Vector3(pos.x, pos.y, 0f);

            // Enable and bump sorting order on all SpriteRenderers so tower renders above walls (sortingOrder 0)
            var renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.enabled = true;
                if (r.sortingOrder < towerSortingOrder)
                {
                    r.sortingOrder = towerSortingOrder;
                }
            }
        }

        private void FitTowerToMount(Entities.TowerBehaviour tower, float mountSize)
        {
            if (tower == null) return;
            mountSize = Mathf.Max(0.01f, mountSize);

            // Collect bounds from all SpriteRenderers to determine the largest local sprite size
            var renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                tower.transform.localScale = new Vector3(mountSize, mountSize, 1f);
                return;
            }

            // Compute the maximum dimension of the combined sprite bounds in local space assuming initial scale
            // Use renderer.sprite.bounds (local space) length to scale uniformly to fit mount square
            float maxDim = 0.001f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sprite == null) continue;
                var b = r.sprite.bounds; // local units before scaling
                maxDim = Mathf.Max(maxDim, b.size.x, b.size.y);
            }

            float uniformScale = mountSize / maxDim;
            tower.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
    }
}
