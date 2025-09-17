using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.UI
{
    public class BuildMenuController : MonoBehaviour
    {
        [SerializeField] private Button towerButtonPrefab;
        [SerializeField] private Transform contentRoot;

        private readonly List<Button> _spawnedButtons = new();

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            Clear();

            foreach (var tower in TowerManager.Instance.UnlockedTowers)
            {
                var button = Instantiate(towerButtonPrefab, contentRoot);
                button.GetComponentInChildren<Text>().text = tower.DisplayName;
                button.onClick.AddListener(() => OnTowerButtonPressed(tower));
                _spawnedButtons.Add(button);
            }
        }

        private void Clear()
        {
            foreach (var button in _spawnedButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }

        private void OnTowerButtonPressed(TowerData tower)
        {
            Systems.PlacementSystem.Instance.QueueTowerPlacement(tower);
        }
    }
}
