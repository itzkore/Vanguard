using UnityEngine;
using BulletHeavenFortressDefense.Fortress;

namespace BulletHeavenFortressDefense.UI
{
    [RequireComponent(typeof(FortressWall))]
    public class WallDamageOverlay : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer overlay;
    [Header("Segmented Overlays")]
    [SerializeField] private bool segmented = true;
    [SerializeField] private SpriteRenderer overlayUL;
    [SerializeField] private SpriteRenderer overlayUR;
    [SerializeField] private SpriteRenderer overlayLL;
    [SerializeField] private SpriteRenderer overlayLR;
    [SerializeField] private SpriteRenderer overlayC;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.55f;
        [SerializeField] private int sortingOrder = 1; // above wall (0), below towers (5)

        private FortressWall _wall;

        private void Awake()
        {
            _wall = GetComponent<FortressWall>();
            if (segmented)
            {
                EnsureQuadrants();
                SetPercents(1f, 1f, 1f, 1f, 1f);
            }
            else
            {
                EnsureOverlay();
                SetPercent(1f);
            }
        }

        private void EnsureOverlay()
        {
            if (overlay != null) return;
            var go = new GameObject("DamageOverlay");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one; // assumes wall is 1x1
            overlay = go.AddComponent<SpriteRenderer>();
            overlay.sprite = CreateSolidSprite(Color.white);
            overlay.sortingOrder = sortingOrder;
            overlay.color = new Color(0, 0, 0, 0);
        }

        private void EnsureQuadrants()
        {
            if (overlayUL == null) overlayUL = CreateQuad("Overlay_UL", new Vector2(-0.25f, 0.25f));
            if (overlayUR == null) overlayUR = CreateQuad("Overlay_UR", new Vector2(0.25f, 0.25f));
            if (overlayLL == null) overlayLL = CreateQuad("Overlay_LL", new Vector2(-0.25f, -0.25f));
            if (overlayLR == null) overlayLR = CreateQuad("Overlay_LR", new Vector2(0.25f, -0.25f));
            if (overlayC == null) overlayC = CreateQuad("Overlay_C", new Vector2(0f, 0f), 0.35f);
        }

        private SpriteRenderer CreateQuad(string name, Vector2 localPos, float scale = 0.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSolidSprite(Color.white);
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(0, 0, 0, 0);
            return sr;
        }

        public void SetPercent(float percent)
        {
            if (segmented)
            {
                // If segmented, treat as setting all parts equally (including center)
                SetPercents(percent, percent, percent, percent, percent);
                return;
            }
            if (overlay == null) EnsureOverlay();
            percent = Mathf.Clamp01(percent);

            // Color goes from green (healthy) -> yellow -> red (destroyed)
            Color c = EvaluateDamageColor(percent);
            // Increase opacity as damage increases (lower percent)
            float damage = 1f - percent;
            c.a = damage * maxAlpha;
            overlay.color = c;
        }

        // Called when the wall reaches final destroyed state; clear overlays so no red remains
        public void SetDestroyedAppearance()
        {
            if (segmented)
            {
                EnsureQuadrants();
                var clear = new Color(0, 0, 0, 0);
                overlayUL.color = clear;
                overlayUR.color = clear;
                overlayLL.color = clear;
                overlayLR.color = clear;
                overlayC.color = clear;
            }
            else
            {
                if (overlay == null) EnsureOverlay();
                overlay.color = new Color(0, 0, 0, 0);
            }
        }

        public void SetPercents(float pUL, float pUR, float pLL, float pLR, float pC)
        {
            if (!segmented)
            {
                SetPercent((pUL + pUR + pLL + pLR + pC) * 0.2f);
                return;
            }

            EnsureQuadrants();
            pUL = Mathf.Clamp01(pUL);
            pUR = Mathf.Clamp01(pUR);
            pLL = Mathf.Clamp01(pLL);
            pLR = Mathf.Clamp01(pLR);
            pC = Mathf.Clamp01(pC);

            overlayUL.color = EvaluateDamageColorWithAlpha(pUL);
            overlayUR.color = EvaluateDamageColorWithAlpha(pUR);
            overlayLL.color = EvaluateDamageColorWithAlpha(pLL);
            overlayLR.color = EvaluateDamageColorWithAlpha(pLR);
            overlayC.color = EvaluateDamageColorWithAlpha(pC);
        }

        private Color EvaluateDamageColor(float percent)
        {
            if (percent >= 0.66f)
            {
                // Healthy to mid
                float t = Mathf.InverseLerp(1f, 0.66f, percent);
                return Color.Lerp(new Color(0.15f, 1f, 0.35f, 1f), new Color(1f, 0.9f, 0.2f, 1f), t);
            }
            else
            {
                // Mid to low
                float t = Mathf.InverseLerp(0.66f, 0f, percent);
                return Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.2f, 0.2f, 1f), t);
            }
        }

        private Color EvaluateDamageColorWithAlpha(float percent)
        {
            var c = EvaluateDamageColor(percent);
            float damage = 1f - Mathf.Clamp01(percent);
            c.a = damage * maxAlpha;
            return c;
        }

        private static Sprite CreateSolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }
    }
}
