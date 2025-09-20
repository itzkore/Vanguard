using UnityEngine;

namespace BulletHeavenFortressDefense.Mobile
{
    [DefaultExecutionOrder(-500)]
    public class MobileOrientationBootstrap : MonoBehaviour
    {
    [SerializeField, Tooltip("Force landscape orientation on mobile platforms.")] private bool forceLandscape = true;
    [SerializeField, Tooltip("Log orientation decisions to console.")] private bool log = true;
    [SerializeField, Tooltip("If true, will set both allowed orientations to LandscapeLeft/Right.")] private bool restrictToLandscape = true;
    [SerializeField, Tooltip("Also apply in editor for testing (uses same code path)." )] private bool simulateInEditor = false;

        private void Awake()
        {
            bool isRuntimeMobile = (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer);
            if (isRuntimeMobile || simulateInEditor)
            {
                ApplyOrientation();
            }
            else if (log)
            {
                Debug.Log("[MobileOrientation] Not a mobile runtime (and simulateInEditor disabled) – no orientation change.");
            }
        }

        private void ApplyOrientation()
        {
            if (!forceLandscape)
            {
                if (log) Debug.Log("[MobileOrientation] forceLandscape disabled – skipping orientation change.");
                return;
            }
            if (log) Debug.Log("[MobileOrientation] Applying landscape orientation (restrict=" + restrictToLandscape + ")");
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            if (restrictToLandscape)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }
    }
}
