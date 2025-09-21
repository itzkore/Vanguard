using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    public class TowerActionPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text investedText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text upgradeLabel;
        [SerializeField] private Button sellButton;
        [SerializeField] private Text sellLabel;
        [SerializeField, Tooltip("Container for generated stat lines")] private RectTransform statsContainer;
        [SerializeField, Tooltip("Prototype or pooled stat line prefab (optional)")] private UpgradeStatLine statLinePrefab;

        private TowerBehaviour _selected;
        private TowerRangeVisualizer _rangeVis; // visual range circle while selected

        private void OnEnable()
        {
            if (EconomySystem.HasInstance)
                EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
        }

        private void OnDisable()
        {
            if (EconomySystem.HasInstance)
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
        }

        public void SelectTower(TowerBehaviour tower)
        {
            if (_selected != null)
            {
                var prevHighlight = _selected.GetComponent<TowerSelectionHighlight>();
                if (prevHighlight != null) prevHighlight.SetSelected(false);
                if (_rangeVis != null) _rangeVis.SetVisible(false);
            }
            _selected = tower;
            if (_selected != null)
            {
                var hl = _selected.GetComponent<TowerSelectionHighlight>();
                if (hl == null) hl = _selected.gameObject.AddComponent<TowerSelectionHighlight>();
                hl.SetSelected(true);
                _rangeVis = _selected.GetComponent<TowerRangeVisualizer>();
                if (_rangeVis == null) _rangeVis = _selected.gameObject.AddComponent<TowerRangeVisualizer>();
                _rangeVis.SetVisible(true);
            }
            RefreshUI();
        }

        public void Deselect()
        {
            if (_selected != null)
            {
                var hl = _selected.GetComponent<TowerSelectionHighlight>();
                if (hl != null) hl.SetSelected(false);
                if (_rangeVis != null) _rangeVis.SetVisible(false);
            }
            _selected = null;
            gameObject.SetActive(false);
        }

        private void RefreshUI()
        {
            if (_selected == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (titleText != null) titleText.text = _selected.DisplayName;
            if (levelText != null) levelText.text = $"Level: {_selected.Level}";
            if (investedText != null) investedText.text = $"Invested: € {_selected.InvestedEnergy}";

            BuildStatsText();
            SetupUpgradeButton();
            SetupSellButton();

            gameObject.SetActive(true);
        }

        private void BuildStatsText()
        {
            if (_selected == null || statsContainer == null) return;

            // Clear existing children
            for (int i = statsContainer.childCount - 1; i >= 0; i--)
                Destroy(statsContainer.GetChild(i).gameObject);

            var td = _selected.Data;
            bool canUp = _selected.CanUpgrade();

            float fr = _selected.CurrentFireRate;
            float rng = _selected.CurrentRange;
            float dmg = _selected.CurrentDamage > 0 ? _selected.CurrentDamage : _selected.BaseDamage;
            float nextFr = _selected.GetNextLevelFireRate();
            float nextDmg = dmg;
            if (canUp)
            {
                nextDmg = _selected.GetNextLevelDamage();
                if (td != null && td.IsSlow) nextDmg *= td.SlowTowerDamageMultiplier;
                if (td != null && td.IsSniper) nextDmg *= td.SniperDamageMultiplier;
            }
            float nextRng = _selected.GetNextLevelRange();
            float dps = dmg * fr;
            float nextDps = nextDmg * nextFr;

            System.Collections.Generic.List<(string label, string value, string nextVal, bool changed)> entries = new();
            const float eps = 0.0001f;
            entries.Add(("FR", fr.ToString("F2"), nextFr.ToString("F2"), canUp && !Mathf.Approximately(fr, nextFr)));
            entries.Add(("Range", rng.ToString("F2"), nextRng.ToString("F2"), canUp && !Mathf.Approximately(rng, nextRng)));
            bool dmgChanged = canUp && Mathf.Abs(nextDmg - dmg) > eps;
            entries.Add(("DMG", dmg.ToString("F2"), nextDmg.ToString("F2"), dmgChanged));
            entries.Add(("DPS", dps.ToString("F2"), nextDps.ToString("F2"), canUp && !Mathf.Approximately(dps, nextDps)));

            if (td != null)
            {
                if (td.IsSplash)
                {
                    float r = _selected.CurrentSplashRadius;
                    float nextR = _selected.GetNextLevelSplashRadius();
                    entries.Add(("AoE R", r.ToString("F2"), nextR.ToString("F2"), canUp && !Mathf.Approximately(r, nextR)));
                    float area = Mathf.PI * r * r;
                    float nextArea = Mathf.PI * nextR * nextR;
                    entries.Add(("AoE A", area.ToString("F1"), nextArea.ToString("F1"), canUp && !Mathf.Approximately(area, nextArea)));
                }
                if (td.IsSlow)
                {
                    float sf = _selected.CurrentSlowFactor;
                    float nextSf = _selected.GetNextLevelSlowFactor();
                    float slowPercent = (1f - sf) * 100f;
                    float nextSlowPercent = (1f - nextSf) * 100f;
                    float dur = _selected.CurrentSlowDuration;
                    float nextDur = _selected.GetNextLevelSlowDuration();
                    int proj = _selected.CurrentProjectilesPerShot;
                    int nextProj = _selected.GetNextLevelProjectilesPerShot();
                    entries.Add(("Slow", slowPercent.ToString("F0")+"%", nextSlowPercent.ToString("F0")+"%", canUp && !Mathf.Approximately(slowPercent, nextSlowPercent)));
                    entries.Add(("SlowDur", dur.ToString("F1"), nextDur.ToString("F1"), canUp && !Mathf.Approximately(dur, nextDur)));
                    entries.Add(("Proj", proj.ToString(), nextProj.ToString(), canUp && proj != nextProj));
                    entries.Add(("Proj/s", (proj*fr).ToString("F2"), (nextProj*nextFr).ToString("F2"), canUp && !Mathf.Approximately(proj*fr, nextProj*nextFr)));
                }
                if (td.IsSniper)
                {
                    int pierce = td.SniperPierceCount;
                    string pierceLabel = pierce < 0 ? "∞" : (pierce == 0 ? "1" : (1 + pierce).ToString());
                    entries.Add(("Pierce", pierceLabel, pierceLabel, false));
                    float cc = td.SniperCritChance * 100f;
                    entries.Add(("Crit%", cc.ToString("F0"), cc.ToString("F0"), false));
                    entries.Add(("CritX", td.SniperCritMultiplier.ToString("F2"), td.SniperCritMultiplier.ToString("F2"), false));
                }
            }

            // Two column layout: left column gets first half (rounded up)
            int total = entries.Count;
            int leftCount = Mathf.CeilToInt(total / 2f);
            // Create two column parents
            var leftCol = new GameObject("ColLeft", typeof(RectTransform));
            var leftRt = leftCol.GetComponent<RectTransform>(); leftRt.SetParent(statsContainer, false); leftRt.anchorMin = new Vector2(0f,1f); leftRt.anchorMax = new Vector2(0f,1f); leftRt.pivot = new Vector2(0f,1f);
            var leftLayout = leftCol.AddComponent<VerticalLayoutGroup>(); leftLayout.spacing = 2f; leftLayout.childAlignment = TextAnchor.UpperLeft; leftLayout.childForceExpandWidth = false; leftLayout.childForceExpandHeight = false;
            var leftLE = leftCol.AddComponent<LayoutElement>(); leftLE.preferredWidth = 560f;

            var rightCol = new GameObject("ColRight", typeof(RectTransform));
            var rightRt = rightCol.GetComponent<RectTransform>(); rightRt.SetParent(statsContainer, false); rightRt.anchorMin = new Vector2(0f,1f); rightRt.anchorMax = new Vector2(0f,1f); rightRt.pivot = new Vector2(0f,1f);
            var rightLayout = rightCol.AddComponent<VerticalLayoutGroup>(); rightLayout.spacing = 2f; rightLayout.childAlignment = TextAnchor.UpperLeft; rightLayout.childForceExpandWidth = false; rightLayout.childForceExpandHeight = false;
            var rightLE = rightCol.AddComponent<LayoutElement>(); rightLE.preferredWidth = 560f;

            Font fnt = titleText != null ? titleText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            int baseSize = titleText != null ? Mathf.RoundToInt(titleText.fontSize * 0.75f) : 14;

            UpgradeStatLine MakeLine(Transform parent)
            {
                UpgradeStatLine line;
                if (statLinePrefab != null)
                {
                    line = Instantiate(statLinePrefab, parent);
                }
                else
                {
                    var go = new GameObject("StatLine", typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    line = go.AddComponent<UpgradeStatLine>();
                }
                return line;
            }

            for (int i = 0; i < total; i++)
            {
                var e = entries[i];
                var parent = i < leftCount ? leftCol.transform : rightCol.transform;
                var line = MakeLine(parent);
                line.Configure(e.label, e.value, e.nextVal, e.changed, fnt, baseSize);
            }
        }

        private void SetupUpgradeButton()
        {
            if (upgradeButton == null || upgradeLabel == null || _selected == null) return;
            int nextCost = _selected.GetNextUpgradeCost();
            bool canUpgrade = _selected.CanUpgrade();
            upgradeLabel.text = canUpgrade ? $"Upgrade (€ {FormatNum(nextCost)})" : "Max Level";
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.interactable = canUpgrade && EconomySystem.HasInstance && EconomySystem.Instance.CurrentEnergy >= nextCost;
            upgradeButton.onClick.AddListener(() =>
            {
                if (_selected.TryUpgrade())
                {
                    RefreshUI();
                    if (_rangeVis != null) _rangeVis.UpdateRadius(_selected.CurrentRange);
                }
            });
        }

        private void SetupSellButton()
        {
            if (sellButton == null || sellLabel == null || _selected == null) return;
            int refund = _selected.GetSellRefund();
            sellLabel.text = $"Sell (+€ {FormatNum(refund)})";
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() =>
            {
                _selected.Sell();
                Deselect();
            });
        }

        private static string FormatNum(int v) => v.ToString("N0");

        private void OnEnergyChanged(int value)
        {
            if (_selected == null || upgradeButton == null) return;
            int nextCost = _selected.GetNextUpgradeCost();
            upgradeButton.interactable = _selected.CanUpgrade() && value >= nextCost;
        }
    }
}
