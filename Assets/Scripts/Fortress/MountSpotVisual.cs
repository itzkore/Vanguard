using UnityEngine;

namespace BulletHeavenFortressDefense.Fortress
{
    [RequireComponent(typeof(FortressMount))]
    public class MountSpotVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private float size = 0.46f; // visual size, slightly smaller to create visible gaps
        [SerializeField] private Color canPlaceColor = new Color(0.2f, 1f, 0.5f, 0.5f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.3f, 0.2f, 0.5f);

        private FortressMount _mount;

        private void Awake()
        {
            _mount = GetComponent<FortressMount>();
            EnsureIndicator();
            SetVisible(false, false);
        }

        private void EnsureIndicator()
        {
            if (indicator != null) return;
            var go = new GameObject("SpotIndicator");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * size;
            indicator = go.AddComponent<SpriteRenderer>();
            indicator.sortingOrder = 10;
            indicator.sprite = CreateSolidSprite(Color.white);
            indicator.enabled = false;
        }

        public float Size => size;

        public void SetSize(float newSize)
        {
            size = Mathf.Max(0.01f, newSize);
            if (indicator == null)
            {
                EnsureIndicator();
            }
            indicator.transform.localScale = Vector3.one * size;
        }

        public void SetVisible(bool visible, bool canPlace)
        {
            if (indicator == null)
            {
                EnsureIndicator();
            }
            indicator.enabled = visible;
            if (visible)
            {
                indicator.color = canPlace ? canPlaceColor : blockedColor;
            }
        }

        private static Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
