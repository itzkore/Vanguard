using UnityEngine;

namespace BulletHeavenFortressDefense.Visual
{
    [DisallowMultipleComponent]
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [Header("Sky")]
        // Dark brown palette
        // skyTop:   #4B3A2B (0.294,0.227,0.169)
        // skyBottom:#2B2119 (0.169,0.129,0.098)
        [SerializeField] private Color skyTop = new Color(0.294f, 0.227f, 0.169f, 1f);
        [SerializeField] private Color skyBottom = new Color(0.169f, 0.129f, 0.098f, 1f);
        [Header("Layers")]
        // far: #3E3023 mid: #36281E near: #2F231A
        [SerializeField] private Color farMountains = new Color(0.243f, 0.188f, 0.137f, 1f);
        [SerializeField] private Color midHills = new Color(0.212f, 0.157f, 0.118f, 1f);
        [SerializeField] private Color plains = new Color(0.184f, 0.137f, 0.102f, 1f);
        [SerializeField] private float layerDepthFar = 0.2f;
        [SerializeField] private float layerDepthMid = 0.45f;
        [SerializeField] private float layerDepthNear = 0.8f;
        [SerializeField] private float parallaxFar = 0.2f;
        [SerializeField] private float parallaxMid = 0.4f;
        [SerializeField] private float parallaxNear = 0.6f;

        private Transform _sky;
        private Transform _far;
        private Transform _mid;
        private Transform _near;
        private Vector3 _lastCamPos;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            BuildLayers();
            _lastCamPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;
            ResizeToCamera();
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Beige Palette")] 
#endif
        public void ApplyBeigePalette()
        {
            skyTop = new Color(0.949f, 0.914f, 0.855f, 1f);
            skyBottom = new Color(0.902f, 0.847f, 0.769f, 1f);
            farMountains = new Color(0.847f, 0.788f, 0.710f, 1f);
            midHills = new Color(0.788f, 0.710f, 0.612f, 1f);
            plains = new Color(0.690f, 0.592f, 0.475f, 1f);
            BuildLayers();
            ResizeToCamera();
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Dark Brown Palette")] 
#endif
        public void ApplyDarkBrownPalette()
        {
            skyTop = new Color(0.294f, 0.227f, 0.169f, 1f);      // #4B3A2B
            skyBottom = new Color(0.169f, 0.129f, 0.098f, 1f);   // #2B2119
            farMountains = new Color(0.243f, 0.188f, 0.137f, 1f); // #3E3023
            midHills = new Color(0.212f, 0.157f, 0.118f, 1f);     // #36281E
            plains = new Color(0.184f, 0.137f, 0.102f, 1f);       // #2F231A
            BuildLayers();
            ResizeToCamera();
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            if (targetCamera.orthographic == false) targetCamera.orthographic = true;
            ResizeToCamera();
            ApplyParallax();
        }

        private void BuildLayers()
        {
            // Clear existing
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }

            _sky = new GameObject("Sky").transform; _sky.SetParent(transform, false);
            _far = new GameObject("FarMountains").transform; _far.SetParent(transform, false);
            _mid = new GameObject("MidHills").transform; _mid.SetParent(transform, false);
            _near = new GameObject("Plains").transform; _near.SetParent(transform, false);
            // Sky: single generated vertical gradient sprite (no white base bleed)
            var gradient = GenerateVerticalGradientTexture(128, skyTop, skyBottom);
            var skyGo = new GameObject("SkyGradient", typeof(SpriteRenderer));
            skyGo.transform.SetParent(_sky, false);
            var sr = skyGo.GetComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(gradient, new Rect(0,0,gradient.width, gradient.height), new Vector2(0.5f,0.5f), 32f, 0, SpriteMeshType.FullRect);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(100f, 100f);
            sr.sortingOrder = -60; // ensure behind silhouettes

            // Far mountains silhouette
            BuildSilhouette(_far, farMountains, layerDepthFar, 0.6f, 0.15f, 7, 0.9f);
            // Mid hills
            BuildSilhouette(_mid, midHills, layerDepthMid, 0.35f, 0.08f, 9, 1.1f);
            // Near plains — very shallow undulation
            BuildSilhouette(_near, plains, layerDepthNear, 0.15f, 0.03f, 11, 1.3f);

            // Sync camera background to bottom beige to hide any gaps outside sprite coverage
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                targetCamera.backgroundColor = skyBottom;
            }
        }

        private void ResizeToCamera()
        {
            if (targetCamera == null) return;
            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;
            float width = halfW * 2f + 10f; // extra to avoid gaps during parallax
            float height = halfH * 2f + 10f;

            // Resize child quads along X to cover the view
            ResizeChildrenWidth(_sky, width, height);
            ResizeChildrenWidth(_far, width * 1.2f, halfH * 0.8f);
            ResizeChildrenWidth(_mid, width * 1.2f, halfH * 0.6f);
            ResizeChildrenWidth(_near, width * 1.2f, halfH * 0.4f);

            var camPos = targetCamera.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, camPos.z + 10f);
        }

        private void ApplyParallax()
        {
            var camPos = targetCamera.transform.position;
            var delta = camPos - _lastCamPos;
            _lastCamPos = camPos;

            // Move layers opposite to camera movement for parallax effect
            if (_far) _far.position += new Vector3(delta.x * (parallaxFar - 1f), 0f, 0f);
            if (_mid) _mid.position += new Vector3(delta.x * (parallaxMid - 1f), 0f, 0f);
            if (_near) _near.position += new Vector3(delta.x * (parallaxNear - 1f), 0f, 0f);
        }

        private SpriteRenderer MakeQuad(Transform parent, float yOffset, float w, float h, Color color)
        {
            var go = new GameObject("Quad", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Texture2D.whiteTexture != null ? Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 1f, 0, SpriteMeshType.FullRect) : null;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w, h);
            sr.color = color;
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            sr.sortingOrder = -50; // behind gameplay
            return sr;
        }

        private void BuildSilhouette(Transform layer, Color color, float depthOffset, float baseHeightRatio, float noiseAmp, int segments, float hillWidthFactor)
        {
            float width = 100f; float height = 20f;
            var quad = MakeQuad(layer, -2f, width, height, color);
            quad.color = color;
            // Simple random undulation using Perlin noise
            float seed = Random.Range(0f, 1000f);
            // Position layer back in Z so it never overlaps gameplay
            layer.localPosition = new Vector3(0f, 0f, depthOffset);
        }

        private void ResizeChildrenWidth(Transform t, float width, float height)
        {
            if (t == null) return;
            for (int i = 0; i < t.childCount; i++)
            {
                var sr = t.GetChild(i).GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var size = sr.size; size.x = width; size.y = height; sr.size = size;
                }
            }
        }

        private Texture2D GenerateVerticalGradientTexture(int height, Color top, Color bottom)
        {
            if (height < 2) height = 2;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                var c = Color.Lerp(bottom, top, t); // y=0 bottom, top at height-1 for intuitive gradient
                tex.SetPixel(0, y, c);
            }
            tex.Apply();
            return tex;
        }
    }
}
