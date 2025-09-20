using System.Collections.Generic;
using System.Collections; // needed for non-generic IEnumerator coroutine signature
using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BulletHeavenFortressDefense.UI
{
    public class BuildMenuController : MonoBehaviour
    {
        [SerializeField] private Button towerButtonPrefab;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 1f, 1f, 0.4f);

        private readonly List<ButtonEntry> _spawnedButtons = new();

        private struct ButtonEntry
        {
            public TowerData Tower;
            public Button Button;
            public Text Label;
            public Image Icon;
        }

        private void Awake()
        {
            if (contentRoot == null)
            {
                // Try to auto-bind to ScrollView/Viewport/Content if present
                var content = transform.Find("ScrollView/Viewport/Content") as RectTransform;
                contentRoot = content != null ? content : (transform as RectTransform);
            }
            Debug.Log($"[BuildMenu] Awake. contentRoot={(contentRoot != null ? contentRoot.name : "<null>")}");

            if (towerButtonPrefab == null)
            {
                towerButtonPrefab = CreateTemplateButton();
                Debug.Log("[BuildMenu] Created template button prefab.");
            }
        }

        private void OnEnable()
        {
            Refresh();
            if (Application.isPlaying)
            {
                // Start delayed retries if list is empty – especially for mobile builds where Resources may finish loading slightly later
                if (_retryRoutine == null)
                {
                    _retryRoutine = StartCoroutine(RetryPopulateIfEmpty());
                }
            }
            if (EconomySystem.HasInstance)
            {
                EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }
            if (TowerManager.HasInstance)
            {
                TowerManager.Instance.TowersChanged += OnTowersChanged;
            }
        }

        private void OnDisable()
        {
            if (EconomySystem.HasInstance)
            {
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
            }
            if (TowerManager.HasInstance)
            {
                TowerManager.Instance.TowersChanged -= OnTowersChanged;
            }
            if (_retryRoutine != null)
            {
                StopCoroutine(_retryRoutine);
                _retryRoutine = null;
            }
        }

        private Coroutine _retryRoutine;
        private const int MAX_RETRY_ATTEMPTS = 6;
        private IEnumerator RetryPopulateIfEmpty()
        {
            // wait a tiny moment for initial scene boot
            yield return new WaitForSeconds(0.25f);
            int attempt = 0;
            while (attempt < MAX_RETRY_ATTEMPTS)
            {
                attempt++;
                bool hasAny = _spawnedButtons.Count > 0;
                if (!hasAny && TowerManager.HasInstance)
                {
                    var mgr = TowerManager.Instance;
                    int before = mgr.UnlockedTowers != null ? mgr.UnlockedTowers.Count : -1;
                    int added = mgr.ForceResourceRescan(logDetails: attempt==1);
                    if (added > 0 || (mgr.UnlockedTowers!=null && mgr.UnlockedTowers.Count>0))
                    {
                        Debug.Log($"[BuildMenu][Retry] Attempt {attempt}: repopulating after rescan (added={added})");
                        Refresh();
                        hasAny = _spawnedButtons.Count > 0;
                    }
                }
                if (hasAny)
                {
                    if (attempt>1) Debug.Log($"[BuildMenu][Retry] Success after {attempt} attempt(s).");
                    break;
                }
                Debug.Log($"[BuildMenu][Retry] Attempt {attempt} found no towers. Waiting...");
                yield return new WaitForSeconds(0.6f);
            }
            _retryRoutine = null;
        }

        public void Refresh()
        {
            if (_refreshInProgress)
            {
                Debug.Log("[BuildMenu] Suppressing nested Refresh call.");
                return;
            }
            _refreshInProgress = true;
            Clear();

            // Ensure UI bindings even if Awake hasn't run yet (ordering safety)
            if (contentRoot == null)
            {
                var content = transform.Find("ScrollView/Viewport/Content") as RectTransform;
                contentRoot = content != null ? content : (transform as RectTransform);
            }
            if (contentRoot == null)
            {
                Debug.LogWarning("[BuildMenu] No contentRoot found. Expected 'ScrollView/Viewport/Content'.");
                return;
            }
            if (towerButtonPrefab == null)
            {
                towerButtonPrefab = CreateTemplateButton();
            }

            // Ensure we have a TowerManager instance
            if (!TowerManager.HasInstance)
            {
                var go = new GameObject("TowerManager");
                go.AddComponent<TowerManager>();
                Debug.Log("[BuildMenu] TowerManager was missing; created new instance.") ;
            }
            // Force lazy resolution so _instance is set even if Awake hasn't run yet
            var manager = TowerManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[BuildMenu] No TowerManager instance after creation attempt.");
                ShowEmptyState("No tower manager found");
                return;
            }

            var towers = manager.UnlockedTowers;
            Debug.Log($"[BuildMenu] Refresh with towers count: {(towers != null ? towers.Count : -1)}");
            List<TowerData> source = null;
            if (towers != null && towers.Count > 0)
            {
                source = new List<TowerData>(towers);
            }
#if UNITY_EDITOR
            else
            {
                // Editor fallback: find all TowerData assets so UI always has content during dev
                source = new List<TowerData>();
                var guids = AssetDatabase.FindAssets("t:TowerData");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<TowerData>(path);
                    if (asset != null && !source.Contains(asset)) source.Add(asset);
                }
                Debug.Log($"[BuildMenu] Editor fallback loaded towers: {source.Count}");
            }
#endif

            if (source == null || source.Count == 0)
            {
                ShowEmptyState("No turrets unlocked");
                return;
            }

            foreach (var tower in source)
            {
                var button = Instantiate(towerButtonPrefab, contentRoot);
                button.gameObject.SetActive(true);
                var label = button.transform.Find("Label")?.GetComponent<Text>();
                var icon = button.transform.Find("Icon")?.GetComponent<Image>();
                Debug.Log($"[BuildMenu] Add card for: {tower?.name} (Display={tower?.DisplayName})");
                if (label != null)
                {
                    int scaled = EconomySystem.HasInstance ? EconomySystem.Instance.GetScaledBuildCost(tower) : tower.BuildCost;
                    label.text = $"{tower.DisplayName} (€ {scaled})";
                }
                if (icon != null)
                {
                    icon.sprite = tower.Icon;
                    icon.enabled = tower.Icon != null;
                    // Simple tint to highlight if no icon
                    if (tower.Icon == null)
                    {
                        icon.color = new Color(1f, 1f, 1f, 0.15f);
                    }
                    else
                    {
                        icon.color = Color.white;
                    }
                }

                button.onClick.AddListener(() => OnTowerButtonPressed(tower));

                _spawnedButtons.Add(new ButtonEntry
                {
                    Tower = tower,
                    Button = button,
                    Label = label,
                    Icon = icon
                });
            }
            _refreshInProgress = false;
        }

        private bool _refreshInProgress = false;

        private void OnTowersChanged()
        {
            // Only auto-refresh if active & enabled (avoid unnecessary instantiations when hidden)
            if (!isActiveAndEnabled) return;
            Debug.Log("[BuildMenu] TowersChanged event received; refreshing build menu.");
            Refresh();
            if (EconomySystem.HasInstance)
            {
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }
        }

        private void OnTowerButtonPressed(TowerData tower)
        {
            PlacementSystem.Instance.QueueTowerPlacement(tower);
            // If we're inside the modal dialog, close the overlay
            var dialog = transform.parent;
            while (dialog != null && dialog.name != "TurretDialog")
            {
                dialog = dialog.parent;
            }
            if (dialog != null)
            {
                dialog.gameObject.SetActive(false);
            }
        }

        private void OnEnergyChanged(int currentEnergy)
        {
            foreach (var entry in _spawnedButtons)
            {
                if (entry.Button == null)
                {
                    continue;
                }

                int scaled = EconomySystem.HasInstance ? EconomySystem.Instance.GetScaledBuildCost(entry.Tower) : entry.Tower.BuildCost;
                bool affordable = currentEnergy >= scaled;
                entry.Button.interactable = affordable;
                if (entry.Label != null)
                {
                    entry.Label.color = affordable ? affordableColor : unaffordableColor;
                }
            }
        }

        private void Clear()
        {
            // Remove any previous empty-state labels
            var empty = contentRoot != null ? contentRoot.Find("EmptyState") : null;
            if (empty != null)
            {
                Destroy(empty.gameObject);
            }
            foreach (var entry in _spawnedButtons)
            {
                if (entry.Button != null)
                {
                    Destroy(entry.Button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }

        private void ShowEmptyState(string message)
        {
            if (contentRoot == null) return;
            var existing = contentRoot.Find("EmptyState");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
            var labelGO = new GameObject("EmptyState", typeof(RectTransform));
            labelGO.transform.SetParent(contentRoot, false);
            var rect = labelGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 48f);
            var text = labelGO.AddComponent<Text>();
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f,1f,1f,0.7f);
            text.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
            text.fontSize = 26;
        }

        private Button CreateTemplateButton()
        {
            var buttonGO = new GameObject("TowerButtonTemplate", typeof(RectTransform));
            buttonGO.transform.SetParent(transform, false);
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(360f, 72f);

            var layoutElement = buttonGO.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 360f;
            layoutElement.minWidth = 280f;
            layoutElement.preferredHeight = 72f;
            layoutElement.minHeight = 64f;

            // Background
            var bg = buttonGO.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.24f, 0.32f, 0.85f);
            var button = buttonGO.AddComponent<Button>();

            // Icon on the left
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(buttonGO.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            iconRect.sizeDelta = new Vector2(52f, 52f);
            var iconImage = iconGO.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            // Label on the right
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(72f, 8f); // leave space for icon
            labelRect.offsetMax = new Vector2(-10f, -8f);

            var text = labelGO.AddComponent<Text>();
            text.text = "Tower (0)";
            text.alignment = TextAnchor.MiddleLeft;
            text.color = affordableColor;
            text.font = BulletHeavenFortressDefense.UI.UIFontProvider.Get();
            text.fontSize = 28;
            text.raycastTarget = false;

            buttonGO.SetActive(false);
            return button;
        }
    }
}
