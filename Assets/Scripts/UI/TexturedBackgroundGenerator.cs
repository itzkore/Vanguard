using UnityEngine;
using UnityEngine.UI;

namespace BulletHeavenFortressDefense.UI
{
    [RequireComponent(typeof(RawImage))]
    public class TexturedBackgroundGenerator : MonoBehaviour
    {
        [SerializeField, Tooltip("Base color (anthracite suggestion: #1A1D21)")] private Color baseColor = new Color(0.1f,0.11f,0.125f,1f);
        [SerializeField, Tooltip("Noise brightness contribution.")] private float noiseIntensity = 0.08f;
        [SerializeField, Tooltip("Noise tile size (smaller = finer pattern). ")] private int noiseScale = 256;
        [SerializeField, Tooltip("Resolution of generated texture.")] private int textureSize = 512;
        [SerializeField, Tooltip("Seed for random noise (-1 = random each run). ")] private int seed = 12345;
        [SerializeField, Tooltip("Regenerate every time in editor play (for iteration). ")] private bool regenerateOnEnable = true;

        private Texture2D _tex;

        private void OnEnable()
        {
            if (regenerateOnEnable) Generate();
        }

        // Called by external scripts to force immediate generation regardless of regenerateOnEnable
        public void ApplyImmediately()
        {
            Generate();
        }

        public void Generate()
        {
            if (_tex != null)
            {
                Destroy(_tex);
            }
            _tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true);
            _tex.filterMode = FilterMode.Bilinear;
            _tex.wrapMode = TextureWrapMode.Repeat;

            int useSeed = seed;
            if (useSeed < 0) useSeed = Random.Range(int.MinValue/2, int.MaxValue/2);
            var prng = new System.Random(useSeed);

            var cols = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    // Simple layered value noise (two octaves of random grid sampling for cheapness)
                    float nx = (float)x / noiseScale;
                    float ny = (float)y / noiseScale;
                    float n = Hash2D(Mathf.FloorToInt(nx), Mathf.FloorToInt(ny));
                    float n2 = Hash2D(Mathf.FloorToInt(nx*2f), Mathf.FloorToInt(ny*2f))*0.5f;
                    float v = Mathf.Clamp01(n*0.6f + n2);
                    float shade = Mathf.Lerp(0f, noiseIntensity, v);
                    Color c = baseColor * (1f - noiseIntensity) + Color.white * shade;
                    cols[y*textureSize + x] = (Color32)c;
                }
            }
            _tex.SetPixels32(cols);
            _tex.Apply(false, false);

            var ri = GetComponent<RawImage>();
            if (ri == null)
            {
                // Safety: in rare cases the RequireComponent might not yet have executed; ensure it's present.
                ri = gameObject.AddComponent<RawImage>();
            }
            ri.color = Color.white;
            ri.texture = _tex;
            ri.uvRect = new Rect(0,0, (float)Screen.width/textureSize, (float)Screen.height/textureSize);
        }

        private static float Hash2D(int x, int y)
        {
            unchecked
            {
                uint h = 2166136261u; // FNV-1a basis
                h ^= (uint)x; h *= 16777619u;
                h ^= (uint)y; h *= 16777619u;
                // final avalanche
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF; // 24-bit mantissa fraction
            }
        }
    }
}
