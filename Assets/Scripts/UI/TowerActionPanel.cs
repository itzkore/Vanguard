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
        [SerializeField, Tooltip("Detailed stats area")] private Text statsText;

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
            if (statsText == null || _selected == null) return;
            var td = _selected.Data;
            bool canUp = _selected.CanUpgrade();

            float fr = _selected.CurrentFireRate;
            float rng = _selected.CurrentRange;
            float dmg = _selected.CurrentDamage > 0 ? _selected.CurrentDamage : _selected.BaseDamage; // current effective damage (includes slow penalty already)
            float nextFr = _selected.GetNextLevelFireRate();
            // Simulate next-level damage including slow tower penalty if applicable
            float nextDmg = dmg;
            if (canUp)
            {
                nextDmg = _selected.GetNextLevelDamage();
                if (td != null && td.IsSlow)
                {
                    nextDmg *= td.SlowTowerDamageMultiplier; // mirror RecalculateStats application order
                }
                if (td != null && (td.IsSniper))
                {
                    nextDmg *= td.SniperDamageMultiplier; // mirror RecalculateStats sniper scaling
                }
            }
            float nextRng = _selected.GetNextLevelRange();
            float dps = dmg * fr;
            float nextDps = nextDmg * nextFr;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            sb.Append($"Level: {_selected.Level} / {td.MaxLevel}\n");

            // Core stats
            sb.Append(canUp ? $"FR: {fr:F2} -> {nextFr:F2}\n" : $"FR: {fr:F2}\n");
            sb.Append(canUp ? $"Range: {rng:F2} -> {nextRng:F2}\n" : $"Range: {rng:F2}\n");
            float diff = nextDmg - dmg;
            const float dmgEps = 0.0001f;
            if (canUp && Mathf.Abs(diff) > dmgEps)
            {
                sb.Append($"Damage: {dmg:F2} -> {nextDmg:F2} (Δ{diff:+0.00;-0.00})\n");
            }
            else
            {
                sb.Append($"Damage: {dmg:F2}\n");
            }
            sb.Append(canUp ? $"DPS: {dps:F2} -> {nextDps:F2}\n" : $"DPS: {dps:F2}\n");

            // Specializations
            if (td != null)
            {
                if (td.IsSplash)
                {
                    float r = _selected.CurrentSplashRadius;
                    float nextR = _selected.GetNextLevelSplashRadius();
                    float area = Mathf.PI * r * r;
                    float nextArea = Mathf.PI * nextR * nextR;
                    sb.Append(canUp ? $"AoE Radius: {r:F2} -> {nextR:F2}\n" : $"AoE Radius: {r:F2}\n");
                    sb.Append(canUp ? $"AoE Area: {area:F1} -> {nextArea:F1}\n" : $"AoE Area: {area:F1}\n");
                    sb.Append($"Falloff Exp: {td.SplashFalloffExponent:F2}\n");
                }
                if (td.IsSlow)
                {
                    float sf = _selected.CurrentSlowFactor;
                    float nextSf = _selected.GetNextLevelSlowFactor();
                    float slowPercent = (1f - sf) * 100f; // percentage of speed reduction
                    float nextSlowPercent = (1f - nextSf) * 100f;
                    float dur = _selected.CurrentSlowDuration;
                    float nextDur = _selected.GetNextLevelSlowDuration();
                    int proj = _selected.CurrentProjectilesPerShot;
                    int nextProj = _selected.GetNextLevelProjectilesPerShot();
                    float projPerSec = proj * fr;
                    float nextProjPerSec = nextProj * nextFr;

                    sb.Append(canUp ? $"Slow: {slowPercent:F0}% -> {nextSlowPercent:F0}%\n" : $"Slow: {slowPercent:F0}%\n");
                    sb.Append(canUp ? $"Slow Dur: {dur:F1}s -> {nextDur:F1}s\n" : $"Slow Dur: {dur:F1}s\n");
                    sb.Append(canUp ? $"Proj/Shot: {proj} -> {nextProj}\n" : $"Proj/Shot: {proj}\n");
                    sb.Append(canUp ? $"Proj/s: {projPerSec:F2} -> {nextProjPerSec:F2}\n" : $"Proj/s: {projPerSec:F2}\n");
                }
                if (td.IsSniper)
                {
                    // Pierce (negative means infinite)
                    int pierce = td.SniperPierceCount;
                    string pierceLabel = pierce < 0 ? "∞" : (pierce == 0 ? "1 target" : (1 + pierce) + " targets");
                    // Since pierce doesn't currently scale per level by design, we just show static
                    sb.Append($"Pierce: {pierceLabel}\n");
                    float cc = td.SniperCritChance * 100f;
                    sb.Append($"Crit: {cc:F0}% x{td.SniperCritMultiplier:F2}\n");
                }
            }

            statsText.text = sb.ToString().TrimEnd('\n');
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
