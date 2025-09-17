using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.UI
{
    public class MetaNodeWidget : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button purchaseButton;

        private MetaUpgradeNode _node;

        public void Bind(MetaUpgradeNode node)
        {
            _node = node;
            if (titleText != null)
            {
                titleText.text = node.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = node.Description;
            }

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(AttemptPurchase);
            }
        }

        private void AttemptPurchase()
        {
            if (_node == null)
            {
                return;
            }

            bool purchased = Systems.MetaProgressionSystem.Instance.TryPurchase(_node);
            if (purchased)
            {
                purchaseButton.interactable = false;
            }
        }
    }
}
