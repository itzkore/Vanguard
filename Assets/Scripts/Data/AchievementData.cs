using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "AchievementData", menuName = "BHFD/Data/Achievement")]
    public class AchievementData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private int reward;

        public string DisplayName => displayName;
        public string Description => description;
        public int Reward => reward;
    }
}
