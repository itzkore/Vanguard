using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.UI
{
    public class MetaTreeView : MonoBehaviour
    {
        [SerializeField] private Text metaCurrencyText;
        [SerializeField] private Transform nodeContainer;
        [SerializeField] private MetaNodeWidget nodePrefab;

        public void Populate(MetaUpgradeTree tree)
        {
            Clear();

            if (tree == null)
            {
                return;
            }

            foreach (var node in tree.Nodes)
            {
                var widget = Instantiate(nodePrefab, nodeContainer);
                widget.Bind(node);
            }
        }

        public void UpdateCurrency(int value)
        {
            if (metaCurrencyText != null)
            {
                metaCurrencyText.text = $"Meta Points: {value}";
            }
        }

        private void Clear()
        {
            for (int i = nodeContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(nodeContainer.GetChild(i).gameObject);
            }
        }
    }
}
