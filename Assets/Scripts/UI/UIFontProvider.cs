using UnityEngine;

namespace BulletHeavenFortressDefense.UI
{
    /// <summary>
    /// Central font provider. Attempts to load Orbitron (Regular / Bold variants) from Resources/Fonts.
    /// Fallback = built-in Arial to avoid missing font exceptions in Editor.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class UIFontProvider : MonoBehaviour
    {
        private static Font _orbitron;
        private static Font _orbitronBold;
        private static bool _attemptedLoad = false;

        [SerializeField, Tooltip("Resource path (under Resources/) to regular Orbitron font asset.")] private string orbitronRegularPath = "Fonts/Orbitron-Regular";
        [SerializeField, Tooltip("Resource path (under Resources/) to bold Orbitron font asset.")] private string orbitronBoldPath = "Fonts/Orbitron-Bold";
        [SerializeField, Tooltip("Log font load process.")] private bool logLoad = false;

        private void Awake()
        {
            if (!_attemptedLoad)
            {
                LoadFonts();
            }
        }

        private void LoadFonts()
        {
            _attemptedLoad = true;
            if (logLoad) Debug.Log("[UIFontProvider] Loading Orbitron fonts...");
            _orbitron = Resources.Load<Font>(orbitronRegularPath);
            _orbitronBold = Resources.Load<Font>(orbitronBoldPath);
            if (logLoad)
            {
                Debug.Log($"[UIFontProvider] Regular={( _orbitron ? _orbitron.name : "NULL") } Bold={( _orbitronBold ? _orbitronBold.name : "NULL") }");
            }
        }

        public static Font Get(bool bold = false)
        {
            // Lazy load if provider not in scene
            if (!_attemptedLoad)
            {
                _orbitron = Resources.Load<Font>("Fonts/Orbitron-Regular");
                _orbitronBold = Resources.Load<Font>("Fonts/Orbitron-Bold");
                _attemptedLoad = true;
            }
            var f = bold ? (_orbitronBold != null ? _orbitronBold : _orbitron) : _orbitron;
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
