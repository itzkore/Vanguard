using UnityEngine;

namespace BulletHeavenFortressDefense.Visuals
{
    [CreateAssetMenu(fileName = "TowerVisualProfile", menuName = "BHFD/Visuals/Tower Visual Profile")] 
    public class TowerVisualProfile : ScriptableObject
    {
        [Header("Identity")] public string towerDisplayName; // Match TowerData.DisplayName
        [Tooltip("Optional explicit match; if empty uses display name.")] public string towerMatchKey;

        [Header("Child Paths (relative to tower root)")]
        public string barrelSpinPath;
        public string recoilRootPath;
        public string glowRendererPath;
        public string muzzlePointPath; // used for beam / muzzle origin alignment
        public string auraRootPath; // optional scaling aura (slow or splash)

        [Header("Prefabs / Effects")] public ParticleSystem muzzleFlashPrefab;
        public ParticleSystem chargePrefab;
        public ParticleSystem impactPrefab; // splash / projectile impact override (optional)
        public ParticleSystem beamPrefab; // sniper beam/tracer
        public ParticleSystem auraPrefab; // if no existing aura child, instantiate this under auraRootPath parent

        [Header("Spin / Recoil / Glow")] public float baseSpinSpeed = 180f;
        public float spinSpeedPerFireRate = 90f;
        public float recoilDistance = 0.12f;
        public float recoilOutTime = 0.04f;
        public float recoilReturnTime = 0.12f;
        [Range(0f,2f)] public float glowPeakAlpha = 1.25f;
        public float glowFadeTime = 0.25f;

        [Header("Charge Logic")] public float minChargeInterval = 0.45f; // seconds
        [Range(0f,1f)] public float chargeLeadFraction = 0.35f;

        [Header("Aura / Radius Scaling")] public bool scaleAuraWithRange = true;
        public float auraBaseVisualRadius = 1f; // baseline radius value = range 1
        public float auraScaleLerpSpeed = 8f; // smoothing
        public float auraMaxAlpha = 0.4f;

        [Header("Color / Emission")] public Color glowColor = Color.cyan;
        public Gradient pulseGradient; // used across glow fade (time 0..1)

        [Header("Pooling Ids (optional)")]
        public string muzzlePoolId;
        public string impactPoolId;
        public string beamPoolId;
        public string auraPoolId;
    }
}
