using System;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class EconomySystem : Singleton<EconomySystem>
    {
    [SerializeField, Tooltip("Starting Euros for a new run.")] private int startingEnergy = 1000; // raised to 1000 per user request
    [Header("Tower Cost Scaling")]
    [SerializeField, Tooltip("Global multiplier applied to tower build cost at placement time (does not modify asset data)." )] private float buildCostGlobalMult = 10f;
    [SerializeField, Tooltip("Global multiplier applied to each upgrade cost (stacked after per-tower growth)." )] private float upgradeCostGlobalMult = 10f; // raised from 1f per balance request
    // NOTE: To approximate ~20x total investment by final level (level 10) you can raise either UpgradeCostGrowth in each TowerData
    // or this upgradeCostGlobalMult. Current logic leaves per-tower growth values intact; adjust in Inspector if cumulative
    // final upgrade feels too cheap/expensive.
    [SerializeField] private GameEvent onEnergyChanged;
    [Header("Run Rewards (Euros)")]
    [SerializeField, Tooltip("Base Euros awarded when a wave completes (before scaling). ")] private int perWaveBonus = 15;
    [SerializeField, Tooltip("Additional percent per wave for the wave-complete bonus. Example: 0.25 = +25% each wave.")] private float perWaveBonusGrowth = 0.25f;
    [SerializeField, Tooltip("Kill reward wave scaling. Example: 0.05 = +5% per wave to the enemy's base reward.")] private float killRewardWaveScale = 0.05f;
    [SerializeField, Tooltip("Minimum Euros granted for a kill after scaling.")] private int minKillReward = 1;

        public int CurrentEnergy { get; private set; }
        public event Action<int> EnergyChanged;
    public float BuildCostGlobalMult => Mathf.Max(0.01f, buildCostGlobalMult);
    public float UpgradeCostGlobalMult => Mathf.Max(0.01f, upgradeCostGlobalMult);

        public int GetScaledBuildCost(BulletHeavenFortressDefense.Data.TowerData tower)
        {
            if (tower == null) return 0;
            return Mathf.RoundToInt(tower.BuildCost * BuildCostGlobalMult);
        }

        public int GetScaledUpgradeCost(int baseCost)
        {
            return Mathf.RoundToInt(baseCost * UpgradeCostGlobalMult);
        }

        protected override void Awake()
        {
            base.Awake();
            // Enforce requested baseline of 1000 even if serialized asset value lower
            if (startingEnergy < 1000)
            {
                startingEnergy = 1000;
            }
            // Enforce minimum upgrade multiplier (balance requirement) – user reported stale low values
            if (upgradeCostGlobalMult < 10f)
            {
                Debug.LogWarning($"[Economy] upgradeCostGlobalMult serialized as {upgradeCostGlobalMult} -> forcing 10.");
                upgradeCostGlobalMult = 10f;
            }
            ResetEnergy();
            Debug.Log($"[Economy] Starting energy set to {startingEnergy}");
        }

        public void SetStartingEnergyRuntime(int amount)
        {
            startingEnergy = Mathf.Max(0, amount);
            Debug.Log($"[Economy] Runtime startingEnergy overridden -> {startingEnergy}");
        }

        private void OnEnable()
        {
            if (BulletHeavenFortressDefense.Managers.WaveManager.HasInstance)
            {
                BulletHeavenFortressDefense.Managers.WaveManager.Instance.WaveCompleted += HandleWaveCompleted;
            }
        }

        private void OnDisable()
        {
            if (BulletHeavenFortressDefense.Managers.WaveManager.HasInstance)
            {
                BulletHeavenFortressDefense.Managers.WaveManager.Instance.WaveCompleted -= HandleWaveCompleted;
            }
        }

        public void ResetEnergy()
        {
            CurrentEnergy = startingEnergy;
            NotifyChanged();
        }

        public bool TrySpend(int amount)
        {
            if (CurrentEnergy < amount)
            {
                return false;
            }

            CurrentEnergy -= amount;
            NotifyChanged();
            return true;
        }

        public void Add(int amount)
        {
            CurrentEnergy += amount;
            NotifyChanged();
        }

        public void AddKillReward(int baseReward)
        {
            int wave = BulletHeavenFortressDefense.Managers.WaveManager.HasInstance ?
                Mathf.Max(1, BulletHeavenFortressDefense.Managers.WaveManager.Instance.CurrentWaveNumber) : 1;
            float scale = 1f + killRewardWaveScale * (wave - 1);
            int amount = Mathf.Max(minKillReward, Mathf.RoundToInt(baseReward * scale));
            Add(amount);
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            if (perWaveBonus > 0)
            {
                float scale = 1f + Mathf.Max(0f, perWaveBonusGrowth) * Mathf.Max(0, waveNumber - 1);
                int bonus = Mathf.Max(0, Mathf.RoundToInt(perWaveBonus * scale));
                Add(bonus);
            }
        }

        private void NotifyChanged()
        {
            onEnergyChanged?.Raise();
            EnergyChanged?.Invoke(CurrentEnergy);
        }
    }
}
