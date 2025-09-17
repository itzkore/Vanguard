using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class EconomySystem : Singleton<EconomySystem>
    {
        [SerializeField] private int startingEnergy = 100;
        [SerializeField] private GameEvent onEnergyChanged;

        public int CurrentEnergy { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            CurrentEnergy = startingEnergy;
        }

        public bool TrySpend(int amount)
        {
            if (CurrentEnergy < amount)
            {
                return false;
            }

            CurrentEnergy -= amount;
            onEnergyChanged?.Raise();
            return true;
        }

        public void Add(int amount)
        {
            CurrentEnergy += amount;
            onEnergyChanged?.Raise();
        }
    }
}
