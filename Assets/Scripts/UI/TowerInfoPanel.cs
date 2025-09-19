using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    public class TowerInfoPanel : MonoBehaviour
    {
        [SerializeField] private Text headerText;
        [SerializeField] private Text statsText;
        [SerializeField] private GameObject contentRoot;

        private TowerBehaviour _current;

        private void Awake()
        {
            if (contentRoot == null) contentRoot = gameObject;
            SetVisible(false);
        }

        private void Update()
        {
            if (_current != null)
            {
                Refresh();
            }
        }

        public void Show(TowerBehaviour tower)
        {
            _current = tower;
            SetVisible(true);
            Refresh();
        }

        public void Hide()
        {
            _current = null;
            SetVisible(false);
        }

        private void SetVisible(bool on)
        {
            if (contentRoot != null && contentRoot.activeSelf != on)
            {
                contentRoot.SetActive(on);
            }
        }

        private void Refresh()
        {
            if (_current == null)
            {
                SetVisible(false);
                return;
            }

            if (headerText != null)
            {
                headerText.text = _current.name;
            }

            if (statsText != null)
            {
                int level = _current.Level;
                int nextCost = _current.GetNextUpgradeCost();
                bool canUpgrade = _current.CanUpgrade();
                // We do not expose raw damage/fireRate after level scaling directly, so approximate from fireCooldown inverse
                // (Could be extended if TowerBehaviour exposed public getters).
                statsText.text = $"Lv {level}\nNext: {(canUpgrade ? ("€ " + nextCost) : "Max")}\nInvested: € {_current.InvestedEnergy}";
            }
        }
    }
}