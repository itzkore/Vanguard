using UnityEngine;
using System.Collections.Generic;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Visuals
{
    [DisallowMultipleComponent]
    public class TowerVisualController : MonoBehaviour
    {
        [Header("Binding")] public TowerBehaviour tower;
        public TowerVisualProfile profile;
        [Tooltip("If true, search Resources for a profile matching tower DisplayName at runtime if profile not assigned.")] public bool autoFindProfile = true;

        // Resolved references
        private Transform _barrel;
        private Transform _recoilRoot;
        private SpriteRenderer _glowRenderer;
        private Transform _muzzlePoint;
        private Transform _auraRoot;
        private ParticleSystem _muzzleFlash;
        private ParticleSystem _chargeEffect;
        private ParticleSystem _beamEffect;
        private ParticleSystem _auraInstance;

        // State
        private float _spinAngle;
        private float _recoilT;
        private bool _recoiling;
        private Vector3 _recoilRest;
        private float _glowT;
        private float _lastFireTime = -999f;
        private float _expectedInterval = 1f;
        private bool _chargePlayed;
        private float _auraCurrentScale = 1f;
        private float _auraTargetScale = 1f;
        private Color _glowBaseColor = Color.white;

        private static readonly List<TowerVisualProfile> _profileCache = new();

        private void Awake()
        {
            if (tower == null) tower = GetComponent<TowerBehaviour>();
            if (profile == null && autoFindProfile && tower != null)
            {
                LoadProfilesIfNeeded();
                string key = tower.DisplayName;
                for (int i = 0; i < _profileCache.Count; i++)
                {
                    var p = _profileCache[i];
                    if (p == null) continue;
                    string match = string.IsNullOrEmpty(p.towerMatchKey) ? p.towerDisplayName : p.towerMatchKey;
                    if (!string.IsNullOrEmpty(match) && match == key)
                    {
                        profile = p;
                        break;
                    }
                }
            }
            if (tower != null)
            {
                tower.Fired += OnTowerFired;
                tower.StatsRecalculated += OnStatsRecalculated;
                if (tower.CurrentFireRate > 0f) _expectedInterval = 1f / tower.CurrentFireRate;
            }
            ApplyProfile();
        }

        private void OnDestroy()
        {
            if (tower != null)
            {
                tower.Fired -= OnTowerFired;
                tower.StatsRecalculated -= OnStatsRecalculated;
            }
        }

        private static void LoadProfilesIfNeeded()
        {
            if (_profileCache.Count > 0) return;
            var loaded = Resources.LoadAll<TowerVisualProfile>(string.Empty);
            if (loaded != null && loaded.Length > 0)
            {
                _profileCache.AddRange(loaded);
            }
        }

        public void ApplyProfile()
        {
            if (profile == null) return;
            // Resolve transforms
            _barrel = ResolvePath(profile.barrelSpinPath);
            _recoilRoot = ResolvePath(profile.recoilRootPath) ?? transform;
            _muzzlePoint = ResolvePath(profile.muzzlePointPath);
            _auraRoot = ResolvePath(profile.auraRootPath);
            if (_recoilRoot != null) _recoilRest = _recoilRoot.localPosition;

            _glowRenderer = ResolvePath(profile.glowRendererPath)?.GetComponent<SpriteRenderer>();
            if (_glowRenderer != null)
            {
                _glowBaseColor = _glowRenderer.color;
                if (profile.glowColor.a > 0f)
                {
                    _glowRenderer.color = profile.glowColor;
                    _glowBaseColor = _glowRenderer.color;
                }
            }

            InstantiateOrAssignEffects();
            UpdateAuraScaleImmediate();
        }

        private Transform ResolvePath(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return null;
            var t = transform.Find(relPath);
            return t;
        }

        private void InstantiateOrAssignEffects()
        {
            if (profile.muzzleFlashPrefab != null)
            {
                _muzzleFlash = Instantiate(profile.muzzleFlashPrefab, _muzzlePoint != null ? _muzzlePoint.position : transform.position, Quaternion.identity, _muzzlePoint != null ? _muzzlePoint : transform);
            }
            if (profile.chargePrefab != null)
            {
                _chargeEffect = Instantiate(profile.chargePrefab, _muzzlePoint != null ? _muzzlePoint.position : transform.position, Quaternion.identity, _muzzlePoint != null ? _muzzlePoint : transform);
                _chargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (profile.beamPrefab != null)
            {
                _beamEffect = Instantiate(profile.beamPrefab, _muzzlePoint != null ? _muzzlePoint.position : transform.position, Quaternion.identity, transform);
                _beamEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (profile.auraPrefab != null && _auraRoot != null)
            {
                _auraInstance = Instantiate(profile.auraPrefab, _auraRoot.position, Quaternion.identity, _auraRoot);
            }
        }

        private void OnTowerFired(TowerBehaviour tb)
        {
            _lastFireTime = Time.time;
            _chargePlayed = false;
            // Recoil
            if (_recoilRoot != null && profile.recoilDistance > 0f)
            {
                _recoiling = true;
                _recoilT = 0f;
            }
            // Glow pulse
            if (_glowRenderer != null && profile.glowFadeTime > 0.01f)
            {
                _glowT = profile.glowFadeTime;
            }
            // Muzzle flash
            if (_muzzleFlash != null)
            {
                _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _muzzleFlash.Play();
            }
            // Beam (sniper style) – simple short play
            if (_beamEffect != null)
            {
                _beamEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _beamEffect.Play();
            }
        }

        private void OnStatsRecalculated(TowerBehaviour tb)
        {
            if (tower != null && tower.CurrentFireRate > 0f)
            {
                _expectedInterval = 1f / tower.CurrentFireRate;
            }
            UpdateAuraTarget();
        }

        private void UpdateAuraTarget()
        {
            if (!profile.scaleAuraWithRange || _auraRoot == null || tower == null) return;
            float range = tower.CurrentRange; // already scaled globally
            if (profile.auraBaseVisualRadius <= 0.0001f) profile.auraBaseVisualRadius = 1f;
            float scale = range / profile.auraBaseVisualRadius;
            _auraTargetScale = Mathf.Max(0.01f, scale);
        }

        private void UpdateAuraScaleImmediate()
        {
            UpdateAuraTarget();
            if (_auraRoot != null)
            {
                _auraCurrentScale = _auraTargetScale;
                _auraRoot.localScale = Vector3.one * _auraCurrentScale;
            }
        }

        private void Update()
        {
            if (profile == null || tower == null) return;

            // Spin
            if (_barrel != null)
            {
                float speed = profile.baseSpinSpeed + profile.spinSpeedPerFireRate * Mathf.Max(0f, tower.CurrentFireRate - 1f);
                _spinAngle = Mathf.Repeat(_spinAngle + speed * Time.deltaTime, 360f);
                _barrel.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
            }

            // Pre-charge
            if (_chargeEffect != null && !_chargePlayed && _expectedInterval >= profile.minChargeInterval)
            {
                float sinceFire = Time.time - _lastFireTime;
                float leadTime = _expectedInterval * (1f - profile.chargeLeadFraction);
                if (sinceFire >= leadTime && sinceFire < _expectedInterval * 0.98f)
                {
                    _chargeEffect.Play();
                    _chargePlayed = true;
                }
            }

            // Recoil anim
            if (_recoiling && _recoilRoot != null)
            {
                _recoilT += Time.deltaTime;
                float outPhase = Mathf.Min(1f, _recoilT / Mathf.Max(0.0001f, profile.recoilOutTime));
                float returnPhase = Mathf.Clamp01((_recoilT - profile.recoilOutTime) / Mathf.Max(0.0001f, profile.recoilReturnTime));
                float curve = outPhase < 1f ? EaseOutQuad(outPhase) : (1f - EaseOutQuad(returnPhase));
                float offset = curve * profile.recoilDistance;
                _recoilRoot.localPosition = _recoilRest + Vector3.left * offset;
                if (_recoilT >= (profile.recoilOutTime + profile.recoilReturnTime))
                {
                    _recoilRoot.localPosition = _recoilRest;
                    _recoiling = false;
                }
            }

            // Glow fade / pulse gradient
            if (_glowRenderer != null && _glowT > 0f)
            {
                _glowT -= Time.deltaTime;
                float f = Mathf.Clamp01(_glowT / profile.glowFadeTime);
                Color baseC = _glowBaseColor;
                if (profile.pulseGradient != null)
                {
                    baseC = profile.pulseGradient.Evaluate(1f - f);
                }
                baseC.a = Mathf.Lerp(_glowBaseColor.a, profile.glowPeakAlpha * _glowBaseColor.a, f);
                _glowRenderer.color = baseC;
            }

            // Aura scaling
            if (_auraRoot != null && profile.scaleAuraWithRange)
            {
                _auraCurrentScale = Mathf.Lerp(_auraCurrentScale, _auraTargetScale, 1f - Mathf.Exp(-profile.auraScaleLerpSpeed * Time.deltaTime));
                _auraRoot.localScale = Vector3.one * _auraCurrentScale;
                if (_auraInstance != null)
                {
                    var main = _auraInstance.main;
                    if (main.startColor.mode == ParticleSystemGradientMode.Color)
                    {
                        // modulate alpha by ratio to keep large radii from being too opaque
                        float alpha = Mathf.Clamp01(profile.auraMaxAlpha / Mathf.Max(1f, _auraCurrentScale * 0.5f));
                        var c = main.startColor.color;
                        c.a = alpha;
                        main.startColor = c;
                    }
                }
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
