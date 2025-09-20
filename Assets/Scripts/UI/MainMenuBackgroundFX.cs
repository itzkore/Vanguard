using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Procedural layered visual effects for the main menu background:
    /// 1. Base static image (assigned externally) – e.g. bgmenu.png
    /// 2. Slow drifting semi-transparent smoke layers (UV panning or position drifting)
    /// 3. Occasional additive flash beams / lens streaks
    /// 4. Lightweight glitch pulses (color channel offset + jitter)
    /// All effects are intentionally cheap: pure UI layering with Images & simple material property tweaks.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuBackgroundFX : MonoBehaviour
    {
        [Header("Base References")] 
        [SerializeField] private Image baseBackground; // required
        [SerializeField, Tooltip("Optional parent RectTransform that will host VFX layers; defaults to background parent.")] private RectTransform fxRoot;

        [Header("Smoke Layers")] 
        [SerializeField] private int smokeLayerCount = 3;
        [SerializeField] private Sprite smokeSprite; // Provide a soft radial / fog sprite (white) for best result
        [SerializeField] private Vector2 smokeBaseScale = new Vector2(1400, 900);
        [SerializeField] private float smokeMinAlpha = 0.10f;
        [SerializeField] private float smokeMaxAlpha = 0.28f;
        [SerializeField] private Vector2 smokeDriftSpeedX = new Vector2(-12f, 12f);
        [SerializeField] private Vector2 smokeDriftSpeedY = new Vector2(-6f, 6f);
        [SerializeField] private Vector2 smokeStartOffsetRange = new Vector2(300f, 180f);

        [Header("Flash Beams")] 
        [SerializeField] private Sprite flashSprite; // thin soft gradient line
        [SerializeField] private float flashIntervalMin = 3f;
        [SerializeField] private float flashIntervalMax = 7f;
        [SerializeField] private Vector2 flashScaleRange = new Vector2(900f, 1500f);
        [SerializeField] private Color flashColor = new Color(1f,0.95f,0.7f,0.65f);
        [SerializeField] private float flashLifetime = 1.4f;

        [Header("Glitch Pulses")] 
        [SerializeField] private bool enableGlitch = true;
        [SerializeField] private float glitchIntervalMin = 4f;
        [SerializeField] private float glitchIntervalMax = 9f;
        [SerializeField] private float glitchDuration = 0.35f;
        [SerializeField] private float glitchMaxPosJitter = 6f;
        [SerializeField] private float glitchMaxRGBShift = 6f; // pixels (simulated by 3 overlay images)
        [SerializeField] private float glitchFlashAlpha = 0.35f;

        [Header("Global Tweaks")] 
        [SerializeField] private int sortingOffset = -5; // ensure below foreground menu UI
        [SerializeField] private bool autoSpawnOnAwake = true;
        [SerializeField] private bool log = false;

        private readonly List<SmokeLayer> _smoke = new();
        private RectTransform _rootRT;
        private float _nextFlashTime = -1f;
        private float _nextGlitchTime = -1f;
        private GlitchBundle _activeGlitch;

        private class SmokeLayer
        {
            public RectTransform rt;
            public Vector2 drift;
            public float baseAlpha;
        }

        private class GlitchBundle
        {
            public Image r;
            public Image g;
            public Image b;
            public float endTime;
        }

        private void Awake()
        {
            if (baseBackground == null)
            {
                baseBackground = GetComponent<Image>();
            }
            _rootRT = (fxRoot != null) ? fxRoot : (baseBackground != null ? baseBackground.rectTransform.parent as RectTransform : transform as RectTransform);
            if (autoSpawnOnAwake)
            {
                EnsureFallbackSprites();
                BuildSmoke();
                ScheduleFlash();
                ScheduleGlitch();
            }
            ApplySortingOffset();
        }

        private void Update()
        {
            TickSmoke();
            TickFlash();
            TickGlitch();
        }

        #region Smoke
        private void BuildSmoke()
        {
            if (smokeSprite == null || baseBackground == null) return;
            for (int i = 0; i < smokeLayerCount; i++)
            {
                var go = new GameObject("Smoke_" + i, typeof(RectTransform));
                go.transform.SetParent(_rootRT, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float ox = Random.Range(-smokeStartOffsetRange.x, smokeStartOffsetRange.x);
                float oy = Random.Range(-smokeStartOffsetRange.y, smokeStartOffsetRange.y);
                rt.anchoredPosition = new Vector2(ox, oy);
                rt.sizeDelta = smokeBaseScale * Random.Range(0.85f, 1.2f);
                var img = go.AddComponent<Image>();
                img.sprite = smokeSprite;
                img.color = new Color(1f,1f,1f, Random.Range(smokeMinAlpha, smokeMaxAlpha));
                img.raycastTarget = false;
                var layer = new SmokeLayer
                {
                    rt = rt,
                    drift = new Vector2(Random.Range(smokeDriftSpeedX.x, smokeDriftSpeedX.y), Random.Range(smokeDriftSpeedY.x, smokeDriftSpeedY.y)),
                    baseAlpha = img.color.a
                };
                _smoke.Add(layer);
                // Place below base background? Actually we want above image but below UI => ensure sibling order
                if (baseBackground != null)
                {
                    go.transform.SetSiblingIndex(baseBackground.transform.GetSiblingIndex()+1);
                }
            }
            if (log) Debug.Log("[MainMenuBackgroundFX] Smoke layers built: " + _smoke.Count);
        }

        private void TickSmoke()
        {
            if (_smoke.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _smoke.Count; i++)
            {
                var s = _smoke[i];
                if (s == null || s.rt == null) continue;
                var pos = s.rt.anchoredPosition;
                pos += s.drift * dt;
                // gentle wrap
                if (pos.x > smokeStartOffsetRange.x) pos.x = -smokeStartOffsetRange.x;
                else if (pos.x < -smokeStartOffsetRange.x) pos.x = smokeStartOffsetRange.x;
                if (pos.y > smokeStartOffsetRange.y) pos.y = -smokeStartOffsetRange.y;
                else if (pos.y < -smokeStartOffsetRange.y) pos.y = smokeStartOffsetRange.y;
                s.rt.anchoredPosition = pos;
            }
        }
        #endregion

        #region Flash
        private void ScheduleFlash()
        {
            _nextFlashTime = Time.unscaledTime + Random.Range(flashIntervalMin, flashIntervalMax);
        }

        private void TickFlash()
        {
            if (flashSprite == null) return;
            if (Time.unscaledTime < _nextFlashTime) return;
            SpawnFlash();
            ScheduleFlash();
        }

        private void SpawnFlash()
        {
            var go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rootRT, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f,0.5f); rt.anchorMax = new Vector2(0.5f,0.5f); rt.pivot = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = new Vector2(Random.Range(-300f,300f), Random.Range(-120f,120f));
            float scale = Random.Range(flashScaleRange.x, flashScaleRange.y);
            rt.sizeDelta = new Vector2(scale, scale*0.18f);
            var img = go.GetComponent<Image>();
            img.sprite = flashSprite;
            img.color = flashColor;
            img.raycastTarget = false;
            // Respect sorting offset relative to base background.
            go.transform.SetSiblingIndex(Mathf.Max(0, baseBackground.transform.GetSiblingIndex() + (sortingOffset >= 0 ? sortingOffset : 2)));
            StartCoroutine(FlashRoutine(go, img));
        }

        // --- Fallback sprite generation ---
        private void EnsureFallbackSprites()
        {
            if (smokeSprite == null)
            {
                smokeSprite = GenerateRadialSoftSprite(256, 256, 0.08f, 1.1f, "FallbackSmoke");
                if (log) Debug.Log("[MainMenuBackgroundFX] Generated fallback smoke sprite.");
            }
            if (flashSprite == null)
            {
                flashSprite = GenerateLinearGradientSprite(512, 64, "FallbackFlash");
                if (log) Debug.Log("[MainMenuBackgroundFX] Generated fallback flash sprite.");
            }
        }

        private Sprite GenerateRadialSoftSprite(int w, int h, float inner, float outer, string name)
        {
            var tex = new Texture2D(w,h, TextureFormat.RGBA32, false, true);
            tex.name = name;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[w*h];
            Vector2 c = new Vector2(w*0.5f, h*0.5f);
            float maxR = Mathf.Min(w,h)*0.5f;
            for (int y=0; y<h; y++)
            {
                for (int x=0; x<w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x,y), c)/maxR;
                    float a = Mathf.InverseLerp(outer, inner, d);
                    a = Mathf.Clamp01(a*a);
                    pixels[y*w+x] = new Color(1f,1f,1f,a);
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0.5f,0.5f), 100f);
        }

        private Sprite GenerateLinearGradientSprite(int w, int h, string name)
        {
            var tex = new Texture2D(w,h, TextureFormat.RGBA32, false, true);
            tex.name = name;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[w*h];
            for (int y=0; y<h; y++)
            {
                float v = Mathf.InverseLerp(0f, h-1, y);
                float a = Mathf.SmoothStep(1f, 0f, v);
                for (int x=0; x<w; x++)
                {
                    pixels[y*w+x] = new Color(1f,1f,1f,a);
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0.5f,0.5f), 100f);
        }

        private System.Collections.IEnumerator FlashRoutine(GameObject go, Image img)
        {
            float start = Time.unscaledTime;
            float end = start + flashLifetime;
            while (Time.unscaledTime < end && img != null)
            {
                float t = Mathf.InverseLerp(start, end, Time.unscaledTime);
                // Ease out brightness
                float a = Mathf.Lerp(1f, 0f, t*t);
                var c = flashColor; c.a *= a; img.color = c;
                go.transform.Rotate(0f,0f, Time.unscaledDeltaTime * 12f);
                yield return null;
            }
            if (go != null) Destroy(go);
        }
        #endregion

        #region Glitch
        private void ScheduleGlitch()
        {
            if (!enableGlitch) return;
            _nextGlitchTime = Time.unscaledTime + Random.Range(glitchIntervalMin, glitchIntervalMax);
        }

        private void TickGlitch()
        {
            if (!enableGlitch || baseBackground == null) return;
            if (Time.unscaledTime >= _nextGlitchTime)
            {
                StartGlitch();
                ScheduleGlitch();
            }

            if (_activeGlitch != null)
            {
                float remaining = _activeGlitch.endTime - Time.unscaledTime;
                if (remaining <= 0f)
                {
                    EndGlitch();
                }
                else
                {
                    float phase = 1f - Mathf.Clamp01(remaining / glitchDuration);
                    float jitter = (1f - phase) * glitchMaxPosJitter;
                    float rgb = (1f - phase) * glitchMaxRGBShift;
                    ApplyGlitchJitter(_activeGlitch, jitter, rgb);
                }
            }
        }

        private void StartGlitch()
        {
            if (_activeGlitch != null) return;
            // Create 3 overlay clones tinted R,G,B each slightly offset
            _activeGlitch = new GlitchBundle();
            _activeGlitch.endTime = Time.unscaledTime + glitchDuration;
            _activeGlitch.r = CreateChannelCopy("Glitch_R", new Color(1f,0.2f,0.2f, glitchFlashAlpha));
            _activeGlitch.g = CreateChannelCopy("Glitch_G", new Color(0.2f,1f,0.2f, glitchFlashAlpha));
            _activeGlitch.b = CreateChannelCopy("Glitch_B", new Color(0.2f,0.6f,1f, glitchFlashAlpha));
        }

        private Image CreateChannelCopy(string name, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(baseBackground.transform.parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = baseBackground.rectTransform.anchorMin;
            rt.anchorMax = baseBackground.rectTransform.anchorMax;
            rt.pivot = baseBackground.rectTransform.pivot;
            rt.anchoredPosition = baseBackground.rectTransform.anchoredPosition;
            rt.sizeDelta = baseBackground.rectTransform.sizeDelta;
            var img = go.GetComponent<Image>();
            img.sprite = baseBackground.sprite;
            img.color = tint;
            img.raycastTarget = false;
            go.transform.SetSiblingIndex(Mathf.Max(0, baseBackground.transform.GetSiblingIndex() + (sortingOffset >= 0 ? sortingOffset+1 : 3)));
            return img;
        }

        private void ApplySortingOffset()
        {
            if (baseBackground == null) return;
            // If sortingOffset is negative we interpret as relative offset BELOW baseBackground, otherwise ABOVE.
            int baseIndex = baseBackground.transform.GetSiblingIndex();
            if (sortingOffset < 0)
            {
                int newIndex = Mathf.Max(0, baseIndex + sortingOffset);
                baseBackground.transform.SetSiblingIndex(newIndex);
            }
            // Positive offsets are applied to spawned children when they are created (see SpawnFlash/CreateChannelCopy).
            // This method ensures the serialized field is always used so compiler doesn't warn.
        }

        private void ApplyGlitchJitter(GlitchBundle g, float jitter, float rgb)
        {
            if (g == null) return;
            // Position jitter & small rotation
            void J(Image img, float xMul, float yMul)
            {
                if (img == null) return;
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(Random.Range(-jitter, jitter)*xMul, Random.Range(-jitter, jitter)*yMul);
                rt.localRotation = Quaternion.Euler(0f,0f, Random.Range(-2f,2f));
            }
            J(g.r,  1f,  1f);
            J(g.g, -1f,  1f);
            J(g.b,  1f, -1f);
        }

        private void EndGlitch()
        {
            if (_activeGlitch == null) return;
            DestroySafe(_activeGlitch.r);
            DestroySafe(_activeGlitch.g);
            DestroySafe(_activeGlitch.b);
            _activeGlitch = null;
        }

        private void DestroySafe(Image img)
        {
            if (img == null) return;
            if (img.gameObject != null) Destroy(img.gameObject);
        }
        #endregion
    }
}
