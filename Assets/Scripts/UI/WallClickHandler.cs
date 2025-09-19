using UnityEngine;
using UnityEngine.EventSystems;
using BulletHeavenFortressDefense.Fortress;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.UI
{
    // Attach to FortressWall gameObjects to open the repair dialog on click
    public class WallClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private FortressWall _wall;
        private WallRepairDialog _dialog;

        private void Awake()
        {
            _wall = GetComponent<FortressWall>();
        }

        public void Configure(WallRepairDialog dialog)
        {
            _dialog = dialog;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_wall == null || _dialog == null)
            {
                return;
            }
            // Show dialog if destroyed OR simply damaged
            bool damaged = _wall.CurrentHealth < _wall.MaxHealth;
            if (!damaged && !_wall.IsDestroyed)
            {
                return; // nothing to repair
            }
            // Only open dialog during build phases; FortressWall.TryRepairAny has its own gate too.
            if (WaveManager.HasInstance)
            {
                var phase = WaveManager.Instance.CurrentPhase;
                bool build = phase == WaveManager.WavePhase.Shop || phase == WaveManager.WavePhase.Preparation;
                if (!build)
                {
                    HUDController.Toast("Repair only between waves");
                    return;
                }
            }
            _dialog.Open(_wall);
        }
    }
}
