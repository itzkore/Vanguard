using UnityEngine;
using UnityEngine.EventSystems;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Managers
{
    public class SelectionManager : Utilities.Singleton<SelectionManager>
    {
        [SerializeField] private UI.TowerActionPanel actionPanel;
        [SerializeField] private LayerMask towerMask;
        [SerializeField] private RectTransform upgradeOverlay;
        [Header("Debug / Selection Tuning")]
        [SerializeField, Tooltip("Enable detailed debug logs for tower selection.")] private bool verboseSelection = true;
        [SerializeField, Tooltip("Extra radius used if direct point hit misses a tower.")] private float fallbackRadius = 0.30f;
        [SerializeField, Tooltip("Cooldown (seconds) after placement during which clicks are ignored.")] private float postPlacementClickCooldown = 0.15f;
    [SerializeField, Tooltip("If no collider found at click, search for closest tower within this radius.")] private float proximitySelectRadius = 0.55f;
    [SerializeField, Tooltip("Enable extremely verbose path tracing logs.")] private bool ultraVerbose = false;
    [SerializeField, Tooltip("Only treat genuinely interactive UI (buttons, sliders, upgrade panel, etc.) as click-blocking; ignore passive full-screen graphics.")] private bool onlyInteractiveUIBlocks = true;
    [SerializeField, Tooltip("If true, tower clicks will NOT be blocked when pointer is over 'Ready/Start' run control button; that button will still receive its own onClick.")] private bool allowThroughRunButton = true;

        private Camera _cam;
    private static readonly System.Collections.Generic.List<RaycastResult> _uiRaycastResults = new();

        private void Start()
        {
            _cam = Camera.main;
            if (towerMask == 0)
            {
                towerMask = Physics2D.AllLayers;
            }
        }

        public void Configure(UI.TowerActionPanel panel)
        {
            actionPanel = panel;
            if (upgradeOverlay == null)
            {
                var hud = FindObjectOfType<UI.HUDController>()?.transform;
                if (hud != null)
                {
                    var overlay = hud.Find("UpgradeDialog") as RectTransform;
                    upgradeOverlay = overlay;
                }
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (PointerBlockedByUI()) return;
                TrySelectAt(Input.mousePosition);
            }
        }

        private bool PointerBlockedByUI()
        {
            if (EventSystem.current == null) return false;

            if (!onlyInteractiveUIBlocks)
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    if (verboseSelection) Debug.Log("[SelectionManager] Click ignored – pointer over UI (broad mode).");
                    return true;
                }
                return false;
            }

            var ped = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(ped, _uiRaycastResults);
            for (int i = 0; i < _uiRaycastResults.Count; i++)
            {
                var go = _uiRaycastResults[i].gameObject;
                if (go == null || !go.activeInHierarchy) continue;
                // Fast allow-list: any object with common interactive components OR our upgrade overlay / action panel root.
                if (IsInteractive(go))
                {
                    if (verboseSelection) Debug.Log($"[SelectionManager] Click ignored – pointer over interactive UI: {go.name}");
                    return true;
                }
            }
            return false;
        }

        private bool IsInteractive(GameObject go)
        {
            // Upgrade overlay or action panel itself
            if (upgradeOverlay != null && go.transform.IsChildOf(upgradeOverlay)) return true;
            if (actionPanel != null && go.transform.IsChildOf(actionPanel.transform)) return true;

            // If configured, allow the RunControlPanel start/ready button to NOT block tower selection.
            if (allowThroughRunButton && go.GetComponent<UnityEngine.UI.Button>() != null)
            {
                // Heuristic: if button label matches typical run control labels, treat it as pass-through for selection (still clickable for itself)
                var txt = go.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null)
                {
                    string s = txt.text;
                    if (s == "Ready" || s == "Start Run" || s == "In Combat")
                    {
                        return false; // don't classify as blocking interactive UI
                    }
                }
            }

            // Common Unity UI selectable / input components
            if (go.GetComponent<UnityEngine.UI.Button>() != null)
            {
                // Allow HUD top-right menu button to not be treated as blocking for world selection logic.
                if (go.name == "TopRightMenuButton" || (go.transform.parent != null && go.transform.parent.name == "TopRightMenuButton"))
                {
                    return false; // Do not count as blocking interactive UI for selection.
                }
                return true;
            }
            if (go.GetComponent<UnityEngine.UI.Toggle>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Slider>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Dropdown>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.ScrollRect>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.InputField>() != null) return true;
#if TMP_PRESENT
            if (go.GetComponent<TMPro.TMP_InputField>() != null) return true;
#endif
            // Any object that explicitly handles pointer clicks
            if (go.GetComponent<IPointerClickHandler>() != null) return true;
            return false;
        }

        private void TrySelectAt(Vector3 screenPos)
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // If we're currently placing or just placed a tower, do not open the upgrade panel yet
            if (Systems.PlacementSystem.HasInstance)
            {
                var ps = Systems.PlacementSystem.Instance;
                if (ps.HasPendingPlacement)
                {
                    if (verboseSelection) Debug.Log("[SelectionManager] Ignoring click – pending placement active.");
                    return; // ignore clicks while placing
                }

                if (Time.time - ps.LastPlacementTime < postPlacementClickCooldown)
                {
                    if (verboseSelection) Debug.Log($"[SelectionManager] Ignoring click – within post placement cooldown ({Time.time - ps.LastPlacementTime:F2}s < {postPlacementClickCooldown}).");
                    return;
                }
            }

            var world = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(_cam.transform.position.z)));
            world.z = 0f; // ensure 2D plane

            if (ultraVerbose) Debug.Log("[SelectionManager] ---- Selection Attempt START ----");
            Collider2D hit = Physics2D.OverlapPoint(world, towerMask);
            if (verboseSelection) Debug.Log($"[SelectionManager] Click world=({world.x:F2},{world.y:F2}) primaryHit={(hit != null ? hit.name : "null")} mask={towerMask.value}");

            TowerBehaviour tower = null;
            if (hit != null && !hit.TryGetComponent<TowerBehaviour>(out tower))
            {
                // Might have clicked a child collider – search parents
                tower = hit.GetComponentInParent<TowerBehaviour>();
            }

            if (tower == null)
            {
                var hits = Physics2D.OverlapCircleAll(world, fallbackRadius, towerMask);
                if (verboseSelection) Debug.Log($"[SelectionManager] Fallback collider search radius={fallbackRadius} -> {hits.Length} colliders");
                for (int i = 0; i < hits.Length && tower == null; i++)
                {
                    var h = hits[i];
                    if (h == null) continue;
                    if (!h.TryGetComponent<TowerBehaviour>(out tower))
                    {
                        tower = h.GetComponentInParent<TowerBehaviour>();
                    }
                    if (tower != null && verboseSelection)
                    {
                        Debug.Log($"[SelectionManager] Fallback collider matched tower via {h.name} (parent {tower.name}).");
                    }
                }
            }

            // Proximity fallback (no colliders struck) – e.g. tower sprite has no collider or mask excludes it
            if (tower == null && proximitySelectRadius > 0f)
            {
                float bestDistSq = float.MaxValue;
                TowerBehaviour best = null;
                var allTowers = TowerManager.HasInstance ? TowerManager.Instance.ActiveTowers : null;
                if (allTowers != null)
                {
                    for (int i = 0; i < allTowers.Count; i++)
                    {
                        var t = allTowers[i];
                        if (t == null) continue;
                        var pos = t.transform.position;
                        float dx = pos.x - world.x;
                        float dy = pos.y - world.y;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < bestDistSq && d2 <= proximitySelectRadius * proximitySelectRadius)
                        {
                            bestDistSq = d2;
                            best = t;
                        }
                    }
                }
                if (best != null)
                {
                    tower = best;
                    if (verboseSelection) Debug.Log($"[SelectionManager] Proximity fallback selected tower {tower.name} (dist={Mathf.Sqrt(bestDistSq):F2} < radius {proximitySelectRadius}).");
                }
                else if (ultraVerbose)
                {
                    Debug.Log($"[SelectionManager] Proximity fallback found no tower within {proximitySelectRadius}.");
                }
            }

            if (tower != null)
            {
                if (verboseSelection) Debug.Log($"[SelectionManager] Tower selected: {tower.name}");
                if (upgradeOverlay != null)
                {
                    upgradeOverlay.SetAsLastSibling();
                    if (!upgradeOverlay.gameObject.activeSelf)
                    {
                        if (verboseSelection) Debug.Log("[SelectionManager] Activating upgrade overlay.");
                        upgradeOverlay.gameObject.SetActive(true);
                    }
                }
                if (actionPanel == null && verboseSelection)
                {
                    Debug.LogWarning("[SelectionManager] actionPanel reference is null – cannot show upgrade info.");
                }
                actionPanel?.gameObject.SetActive(true);
                actionPanel?.SelectTower(tower);
                // tower stats now integrated into enlarged upgrade panel
            }
            else
            {
                if (verboseSelection) Debug.Log("[SelectionManager] No tower found – clearing selection if open.");
                if (upgradeOverlay != null && upgradeOverlay.gameObject.activeSelf)
                {
                    actionPanel?.Deselect();
                    upgradeOverlay.gameObject.SetActive(false);
                }
            }
            if (ultraVerbose) Debug.Log("[SelectionManager] ---- Selection Attempt END ----");
        }
    }
}

