using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    public class WallRepairDialog : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text costText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private FortressWall _target;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirm);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Close);
            }
        }

        public void Open(FortressWall wall)
        {
            _target = wall;
            if (wall == null)
            {
                Close();
                return;
            }
            // If already full health -> don't even open (user nechce zbytečný panel)
            if (wall.CurrentHealth >= wall.MaxHealth)
            {
                Close();
                return;
            }

            if (titleText != null)
            {
                // Minimal title format: just coordinates
                titleText.text = $"R{wall.Row}C{wall.Column}";
            }
            if (costText != null) RefreshCostLine();
            // Health line removed (minimal dialog)
            transform.parent?.gameObject.SetActive(true);
        }

        public void Close()
        {
            transform.parent?.gameObject.SetActive(false);
            _target = null;
        }

        private void OnConfirm()
        {
            if (_target == null)
            {
                Close();
                return;
            }
            // Try repair; FortressWall handles spending and validation (full or partial)
            bool ok = _target.TryRepairAny();
            Close();
        }

        private void Update()
        {
            if (_target != null && gameObject.activeInHierarchy)
            {
                RefreshCostLine();
            }
        }

        private void RefreshCostLine()
        {
            if (_target == null || costText == null) return;
            int energy = EconomySystem.HasInstance ? EconomySystem.Instance.CurrentEnergy : 0;
            int cost = _target.GetRepairCostForMissing();
            bool can = cost > 0 && energy >= cost;
            if (cost <= 0)
            {
                // Auto-close if we somehow got here while already full
                Close();
                return;
            }
            costText.text = $"€ {cost} / € {energy}"; // compact line
            if (confirmButton != null) confirmButton.interactable = can;
        }
    }
}
