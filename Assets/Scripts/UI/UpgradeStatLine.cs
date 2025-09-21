using UnityEngine;
using UnityEngine.UI;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Reusable UI line for displaying a tower upgrade stat (current -> next) with color coding.
    /// Layout: [Label] [Current] [Arrow] [Next]
    /// If no change, only Label + Current are shown.
    /// </summary>
    public class UpgradeStatLine : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Text currentValueText;
        [SerializeField] private Text arrowText;
        [SerializeField] private Text nextValueText;

        private Color _improveColor = new Color(0.55f, 0.95f, 0.55f, 1f); // soft green
        private Color _baseColor = Color.white;

        /// <summary>
        /// Build the internal text components (called automatically if missing).
        /// </summary>
        public void EnsureBuilt(Font font, int fontSize)
        {
            if (labelText != null) return; // already built

            var rt = GetComponent<RectTransform>();
            if (rt == null) gameObject.AddComponent<RectTransform>();

            HorizontalLayoutGroup h; if (!TryGetComponent(out h)) h = gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f; h.childAlignment = TextAnchor.MiddleLeft; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            Text Make(string name, float sizeMult = 1f, TextAnchor anchor = TextAnchor.MiddleLeft)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<Text>();
                t.font = font; t.color = _baseColor; t.alignment = anchor; t.fontSize = Mathf.RoundToInt(fontSize * sizeMult);
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                return t;
            }

            labelText = Make("Label", 1f);
            currentValueText = Make("Current", 1f);
            arrowText = Make("Arrow", 1f, TextAnchor.MiddleCenter);
            nextValueText = Make("Next", 1f);
        }

        public void Configure(string label, string currentVal, string nextVal, bool changed, Font font, int fontSize)
        {
            EnsureBuilt(font, fontSize);

            labelText.text = label + ":";
            currentValueText.text = currentVal;

            if (!changed)
            {
                arrowText.gameObject.SetActive(false);
                nextValueText.gameObject.SetActive(false);
                currentValueText.color = _baseColor;
            }
            else
            {
                arrowText.gameObject.SetActive(true);
                nextValueText.gameObject.SetActive(true);
                arrowText.text = "→"; // simple arrow glyph
                nextValueText.text = nextVal;
                nextValueText.color = _improveColor;
                currentValueText.color = _baseColor * 0.9f;
            }
        }
    }
}
