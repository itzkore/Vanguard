using UnityEngine;
using BulletHeavenFortressDefense.Utilities;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Systems
{
    public class MetaProgressionSystem : Singleton<MetaProgressionSystem>
    {
        [SerializeField] private int startingMetaCurrency = 0;
        [SerializeField] private GameEvent onMetaCurrencyChanged;

        public int MetaCurrency { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            MetaCurrency = startingMetaCurrency;
        }

        public void GrantCurrency(int amount)
        {
            MetaCurrency += amount;
            onMetaCurrencyChanged?.Raise();
        }

        public bool TryPurchase(MetaUpgradeNode node)
        {
            if (node == null || MetaCurrency < node.Cost)
            {
                return false;
            }

            MetaCurrency -= node.Cost;
            node.Apply();
            onMetaCurrencyChanged?.Raise();
            return true;
        }
    }
}
