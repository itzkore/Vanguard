using UnityEngine;

namespace BulletHeavenFortressDefense.Preload
{
    /// <summary>
    /// Najde Preloader na scéně nebo vytvoří nový a spustí ho s nalezeným / zadaným configem.
    /// Užitečné pokud nechceš ručně nastavovat ve scéně – stačí přidat tento skript.
    /// </summary>
    public class AutoPreloader : MonoBehaviour
    {
        [Tooltip("Volitelný explicitní config. Pokud není zadán, zkusí Resources.Load<PreloadConfig>(configResourcePath)." )]
        public PreloadConfig explicitConfig;
        [Tooltip("Resources cesta (bez prefixu) pro automatické nalezení configu, např. 'Preload/PreloadConfig_Main'.")] public string configResourcePath = "PreloadConfig";
        [Tooltip("Pokud Preloader už existuje, přepíše mu config (pokud explicit není null)." )] public bool overrideExistingConfig = true;
        [Tooltip("Spustit pouze pokud ještě nebyl spuštěn (kontroluje interní flag Preloaderu).")] public bool onlyIfNotStarted = true;
        [Tooltip("Logování.")] public bool verbose = true;

        private void Start()
        {
            var preloader = FindObjectOfType<Preloader>();
            if (preloader == null)
            {
                var go = new GameObject("~PreloaderRuntime");
                preloader = go.AddComponent<Preloader>();
                if (verbose) Debug.Log("[AutoPreloader] Created runtime Preloader instance.");
            }

            if (explicitConfig == null && !string.IsNullOrWhiteSpace(configResourcePath))
            {
                explicitConfig = Resources.Load<PreloadConfig>(configResourcePath);
                if (verbose) Debug.Log("[AutoPreloader] Loaded config from Resources: " + (explicitConfig != null ? explicitConfig.name : "<null>"));
            }

            if (explicitConfig != null && overrideExistingConfig)
            {
                preloader.SetConfig(explicitConfig);
            }

            // Pokus o spuštění (využij kontext menu v Preloaderu pokud autoStart false)
            if (verbose) Debug.Log("[AutoPreloader] Attempt StartPreload()");
            preloader.StartPreload();
        }
    }
}
