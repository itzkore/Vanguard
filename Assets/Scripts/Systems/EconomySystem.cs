using System;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class EconomySystem : Singleton<EconomySystem>
    {
        [SerializeField] private int startingEnergy = 100;
        [SerializeField] private GameEvent onEnergyChanged;

        public int CurrentEnergy { get; private set; }
        public event Action<int> EnergyChanged;

        protected override void Awake()
        {
            base.Awake();
            ResetEnergy();
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

        private void NotifyChanged()
        {
            onEnergyChanged?.Raise();
            EnergyChanged?.Invoke(CurrentEnergy);
        }
    }
}
