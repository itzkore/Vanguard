using UnityEngine;
using UnityEngine.UI;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Attach to the Build Menu window (the parent that contains the header + energy label + scroll view).
    /// Ensures a header text reading "Select Turret" exists as the first child. Recreates it if missing (e.g. stripped by layout changes).
    /// </summary>
    [ExecuteAlways]
    public class EnsureSelectHeader : MonoBehaviour
    {
        [SerializeField] private string headerText = "Select Turret";
        [SerializeField] private int fontSize = 42;
        [SerializeField] private bool forceOnEnable = true;
        [SerializeField, Tooltip("If true, will rename any existing first Text child to match header text.")] private bool normalizeExisting = true;
        [SerializeField] private bool verbose = false;

        private void OnEnable()
        {
            if (forceOnEnable) EnsureHeader();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnsureHeader();
            }
#endif
        }

        private void EnsureHeader()
        {
            // Look for any Text component child whose name suggests it's the header
            Text existing = null;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var txt = child.GetComponent<Text>();
                if (txt == null) continue;
                string n = child.name.ToLower();
                if (n.Contains("select") || n.Contains("header") || n.Contains("title"))
                {
                    existing = txt;
                    break;
                }
            }

            if (existing == null && transform.childCount > 0)
            {
                // Maybe first child is header but renamed; inspect its Text
                var first = transform.GetChild(0).GetComponent<Text>();
                if (first != null) existing = first;
            }

            if (existing == null)
            {
                var go = new GameObject("Header_SelectTurret", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                go.transform.SetSiblingIndex(0);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(400f, 60f);
                var txt = go.AddComponent<Text>();
                txt.text = headerText;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.fontSize = fontSize;
                txt.font = UIFontProvider.Get();
                txt.color = Color.white;
                if (verbose) Debug.Log("[EnsureSelectHeader] Created missing header.", this);
            }
            else
            {
                if (normalizeExisting)
                {
                    existing.text = headerText;
                    existing.fontSize = fontSize;
                    if (existing.font == null) existing.font = UIFontProvider.Get();
                    if (verbose) Debug.Log("[EnsureSelectHeader] Normalized existing header.", this);
                }
            }
        }
    }
}
