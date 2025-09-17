using System;
using System.Collections.Generic;
using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "MetaUpgradeTree", menuName = "BHFD/Data/Meta Upgrade Tree")]
    public class MetaUpgradeTree : ScriptableObject
    {
        [SerializeField] private List<MetaUpgradeNode> nodes = new();

        public IReadOnlyList<MetaUpgradeNode> Nodes => nodes;
    }

    [Serializable]
    public class MetaUpgradeNode
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private int cost = 10;
        [SerializeField] private List<string> prerequisites = new();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int Cost => cost;
        public IReadOnlyList<string> Prerequisites => prerequisites;

        public void Apply()
        {
            Debug.Log($"Meta upgrade {displayName} applied.");
        }
    }
}
