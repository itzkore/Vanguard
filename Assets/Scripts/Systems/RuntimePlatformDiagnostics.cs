using UnityEngine;

namespace BulletHeavenFortressDefense.Systems
{
    /// <summary>
    /// Ensures the game is in landscape orientation at runtime and logs active graphics API (Vulkan vs GLES3).
    /// Optionally can force a relaunch/layout adjust if orientation was incorrect.
    /// </summary>
    public sealed class RuntimePlatformDiagnostics : MonoBehaviour
    {
        [Tooltip("If true, will enforce landscape orientation flags on start.")] public bool enforceLandscape = true;
        [Tooltip("Log graphics device + Vulkan support info on start.")] public bool logGraphicsInfo = true;
        [Tooltip("If device starts in portrait, rotate immediately by setting Screen.orientation.")] public bool forceRotateIfPortrait = true;
        [Tooltip("If true, will also log safe area & aspect ratio for UI tuning.")] public bool logSafeArea = true;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (enforceLandscape)
            {
                // Allow only landscape orientations
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.orientation = ScreenOrientation.AutoRotation;

                if (forceRotateIfPortrait && (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown))
                {
                    // Nudge to a specific landscape first; device may then stay in autorotation
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                }
            }

            if (logGraphicsInfo)
            {
                var deviceType = SystemInfo.graphicsDeviceType;
                bool vulkan = deviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
                string apiList = "unknown";
#if UNITY_2021_2_OR_NEWER
                // Some later versions expose GetGraphicsAPIs; use reflection to stay safe if stripped
                try
                {
                    var m = typeof(SystemInfo).GetMethod("GetGraphicsAPIs", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (m != null)
                    {
                        var arr = m.Invoke(null, new object[] { RuntimePlatform.Android }) as UnityEngine.Rendering.GraphicsDeviceType[];
                        if (arr != null) apiList = string.Join(",", arr);
                    }
                }
                catch { /* ignore */ }
#endif
                Debug.Log($"[RuntimePlatformDiagnostics] Active API: {deviceType} (IsVulkan={vulkan}) SupportedAPIs(Android)=[{apiList}] Vendor={SystemInfo.graphicsDeviceVendor} Version={SystemInfo.graphicsDeviceVersion}");
            }

            if (logSafeArea)
            {
                var sa = Screen.safeArea; // in pixels
                float aspect = (float)Screen.width / Screen.height;
                Debug.Log($"[RuntimePlatformDiagnostics] Resolution={Screen.width}x{Screen.height} Aspect={aspect:F3} SafeArea=({sa.x},{sa.y},{sa.width},{sa.height}) DPI={Screen.dpi}");
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorAutoInject()
        {
            // Ensure a single instance exists in the first scene while working in editor play mode
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredPlayMode)
                {
                    if (FindObjectOfType<RuntimePlatformDiagnostics>() == null)
                    {
                        var go = new GameObject("~RuntimePlatformDiagnostics");
                        go.AddComponent<RuntimePlatformDiagnostics>();
                        Debug.Log("[RuntimePlatformDiagnostics] Auto-injected for editor play mode.");
                    }
                }
            };
        }
#endif
    }
}
