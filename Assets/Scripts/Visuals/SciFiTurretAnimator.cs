using UnityEngine;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Visuals
{
    /// <summary>
    /// Attachable component for the new Sci-Fi turret prefab that animates barrel spin, muzzle glow pulse,
    /// optional charge-up particle, and recoil when the tower fires. Requires a TowerBehaviour sibling.
    /// Design goals:
    ///  - Zero GC allocations per frame
    ///  - Graceful degrade if references not assigned
    ///  - Scales effects by fire rate (faster FR -> faster spin & shorter pulse)
    /// </summary>
    [DisallowMultipleComponent]
    public class SciFiTurretAnimator : MonoBehaviour
    {
        [Header("References")] public TowerBehaviour tower;
        [Tooltip("Object to rotate for continuous barrel spin.")] public Transform barrelSpinRoot;
        [Tooltip("Object (or group) to move slightly backwards for recoil when firing.")] public Transform recoilRoot;
        [Tooltip("Emission / glow sprite (optional). Color/alpha will be pulsed on fire.")] public SpriteRenderer glowSprite;
        [Tooltip("Charge-up particle system played slightly before firing if fire interval is long enough.")] public ParticleSystem chargeParticle;
        [Tooltip("Muzzle flash particle triggered exactly on fire event.")] public ParticleSystem muzzleFlash;

        [Header("Tuning")] [Tooltip("Base spin speed in degrees/sec at fireRate=1.")] public float baseSpinSpeed = 180f;
        [Tooltip("Extra spin speed scaling per +1 fireRate (additive).")] public float spinSpeedPerFireRate = 90f;
        [Tooltip("Recoil distance (local -X).")] public float recoilDistance = 0.12f;
        [Tooltip("Seconds recoil travels out.")] public float recoilOutTime = 0.04f;
        [Tooltip("Seconds recoil returns.")] public float recoilReturnTime = 0.12f;
        [Tooltip("Glow max intensity multiplier (alpha).")] [Range(0f,2f)] public float glowPeakAlpha = 1.25f;
        [Tooltip("Seconds for glow to fade back to original alpha.")] public float glowFadeTime = 0.25f;
        [Tooltip("Minimum fire interval required to play pre-charge effect (seconds). Lower disables charge pre-cue.")] public float minChargeInterval = 0.45f;
        [Tooltip("How many seconds before predicted fire to play charge particle (fraction of interval if >0).")] [Range(0f,1f)] public float chargeLeadFraction = 0.35f;

        private float _spinAngle;
        private float _recoilT;
        private bool _recoiling;
        private Vector3 _recoilRestLocalPos;
        private float _glowFadeT;
        private float _glowBaseAlpha = 1f;
        private float _lastFireTime = -999f;
        private float _expectedInterval = 1f;
        private bool _chargeArmed;

        private void Awake()
        {
            if (tower == null) tower = GetComponent<TowerBehaviour>();
            if (tower != null)
            {
                tower.Fired += HandleTowerFired;
                // Initial interval guess from current fire rate
                if (tower.CurrentFireRate > 0f) _expectedInterval = 1f / tower.CurrentFireRate;
            }
            if (recoilRoot != null) _recoilRestLocalPos = recoilRoot.localPosition;
            if (glowSprite != null)
            {
                _glowBaseAlpha = glowSprite.color.a;
            }
        }

        private void OnDestroy()
        {
            if (tower != null) tower.Fired -= HandleTowerFired;
        }

        private void Update()
        {
            if (tower != null && tower.CurrentFireRate > 0f)
            {
                _expectedInterval = 1f / tower.CurrentFireRate;
            }

            // Barrel spin
            if (barrelSpinRoot != null)
            {
                float speed = baseSpinSpeed + spinSpeedPerFireRate * (tower != null ? Mathf.Max(0f, tower.CurrentFireRate - 1f) : 0f);
                _spinAngle = Mathf.Repeat(_spinAngle + speed * Time.deltaTime, 360f);
                barrelSpinRoot.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
            }

            // Pre-charge scheduling
            if (!_chargeArmed && chargeParticle != null && tower != null && _expectedInterval >= minChargeInterval)
            {
                float sinceFire = Time.time - _lastFireTime;
                float leadTime = _expectedInterval * (1f - chargeLeadFraction);
                if (sinceFire >= leadTime && sinceFire < _expectedInterval * 0.98f)
                {
                    chargeParticle.Play();
                    _chargeArmed = true;
                }
            }

            // Recoil animation
            if (_recoiling && recoilRoot != null)
            {
                _recoilT += Time.deltaTime;
                float outPhase = Mathf.Min(1f, _recoilT / Mathf.Max(0.0001f, recoilOutTime));
                float returnPhase = Mathf.Clamp01((_recoilT - recoilOutTime) / Mathf.Max(0.0001f, recoilReturnTime));
                float curve = outPhase < 1f ? EaseOutQuad(outPhase) : (1f - EaseOutQuad(returnPhase));
                float offset = curve * recoilDistance;
                recoilRoot.localPosition = _recoilRestLocalPos + Vector3.left * offset;
                if (_recoilT >= (recoilOutTime + recoilReturnTime))
                {
                    recoilRoot.localPosition = _recoilRestLocalPos;
                    _recoiling = false;
                }
            }

            // Glow fade
            if (glowSprite != null && _glowFadeT > 0f)
            {
                _glowFadeT -= Time.deltaTime;
                float f = Mathf.Clamp01(_glowFadeT / glowFadeTime);
                Color c = glowSprite.color;
                c.a = Mathf.Lerp(_glowBaseAlpha, _glowBaseAlpha * glowPeakAlpha, f);
                glowSprite.color = c;
            }
        }

        private void HandleTowerFired(TowerBehaviour tb)
        {
            _lastFireTime = Time.time;
            _chargeArmed = false; // reset for next pre-charge
            // Recoil trigger
            if (recoilRoot != null)
            {
                _recoiling = true;
                _recoilT = 0f;
            }
            // Glow pulse
            if (glowSprite != null)
            {
                _glowFadeT = glowFadeTime;
            }
            // Muzzle flash
            if (muzzleFlash != null)
            {
                muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlash.Play();
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
