using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Systems;

namespace BulletHeavenFortressDefense.UI
{
    public class BuildMenuController : MonoBehaviour
    {
        [SerializeField] private Button towerButtonPrefab;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 1f, 1f, 0.4f);

        private readonly List<ButtonEntry> _spawnedButtons = new();

        private struct ButtonEntry
        {
            public TowerData Tower;
            public Button Button;
            public Text Label;
        }

        private void Awake()
        {
            if (contentRoot == null)
            {
                contentRoot = transform as RectTransform;
            }

            if (towerButtonPrefab == null)
            {
                towerButtonPrefab = CreateTemplateButton();
            }
        }

        private void OnEnable()
        {
            Refresh();
            if (EconomySystem.HasInstance)
            {
                EconomySystem.Instance.EnergyChanged += OnEnergyChanged;
                OnEnergyChanged(EconomySystem.Instance.CurrentEnergy);
            }
        }

        private void OnDisable()
        {
            if (EconomySystem.HasInstance)
            {
                EconomySystem.Instance.EnergyChanged -= OnEnergyChanged;
            }
        }

        public void Refresh()
        {
            Clear();

            if (!TowerManager.HasInstance)
            {
                return;
            }

            foreach (var tower in TowerManager.Instance.UnlockedTowers)
            {
                var button = Instantiate(towerButtonPrefab, contentRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = $"{tower.DisplayName} ({tower.BuildCost})";
                }

                button.onClick.AddListener(() => OnTowerButtonPressed(tower));

                _spawnedButtons.Add(new ButtonEntry
                {
                    Tower = tower,
                    Button = button,
                    Label = label
                });
            }
        }

        private void OnTowerButtonPressed(TowerData tower)
        {
            PlacementSystem.Instance.QueueTowerPlacement(tower);
        }

        private void OnEnergyChanged(int currentEnergy)
        {
            foreach (var entry in _spawnedButtons)
            {
                if (entry.Button == null)
                {
                    continue;
                }

                bool affordable = currentEnergy >= entry.Tower.BuildCost;
                entry.Button.interactable = affordable;
                if (entry.Label != null)
                {
                    entry.Label.color = affordable ? affordableColor : unaffordableColor;
                }
            }
        }

        private void Clear()
        {
            foreach (var entry in _spawnedButtons)
            {
                if (entry.Button != null)
                {
                    Destroy(entry.Button.gameObject);
                }
            }

            _spawnedButtons.Clear();
        }

        private Button CreateTemplateButton()
        {
            var buttonGO = new GameObject("TowerButtonTemplate", typeof(RectTransform));
            buttonGO.transform.SetParent(transform, false);
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(240f, 48f);

            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.32f, 0.85f);
            var button = buttonGO.AddComponent<Button>();

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            var text = labelGO.AddComponent<Text>();
            text.text = "Tower";
            text.alignment = TextAnchor.MiddleLeft;
            text.color = affordableColor;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 28;

            buttonGO.SetActive(false);
            return button;
        }
    }
}
