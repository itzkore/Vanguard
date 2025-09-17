using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "UpgradeData", menuName = "BHFD/Data/Upgrade")]
    public class UpgradeData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private int cost;

        public string DisplayName => displayName;
        public string Description => description;
        public int Cost => cost;
    }
}
