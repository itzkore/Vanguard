using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Managers;

namespace BulletHeavenFortressDefense.Preload
{
    public class Preloader : MonoBehaviour
    {
        [SerializeField] private PreloadConfig config;
        public void SetConfig(PreloadConfig newConfig)
        {
            if (_started) { Debug.LogWarning("[Preloader] Cannot change config after start."); return; }
            config = newConfig;
        }
        [Header("Progress UI (optional)")]
        [SerializeField] private UnityEngine.UI.Slider progressSlider;
        [SerializeField] private UnityEngine.UI.Text progressLabel;
        [SerializeField, Tooltip("Scene name nebo prázdné pokud jen přepneme GameManager state")] private string nextSceneName = "";
        [SerializeField, Tooltip("Pokud nepřepínáme scénu a GameManager je k dispozici, přepni rovnou do MainMenu.")] private bool switchToMainMenuState = true;
        [SerializeField, Tooltip("Automaticky začít po Awake.")] private bool autoStart = true;
        [SerializeField, Tooltip("Verbose log výstup.")] private bool verbose = true;

        private bool _started;

        private void Awake()
        {
            if (autoStart)
            {
                StartPreload();
            }
        }

        [ContextMenu("Start Preload (Editor)")]
        public void StartPreload()
        {
            if (_started) return;
            if (config == null)
            {
                Debug.LogWarning("[Preloader] Missing config.");
                return;
            }
            _started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var steps = CountSteps();
            int done = 0;
            UpdateProgress(done, steps, "Inicializace");
            yield return null;

            // 1. Direct assets (touch to force load refs)
            if (config.directAssets != null)
            {
                for (int i = 0; i < config.directAssets.Count; i++)
                {
                    var a = config.directAssets[i];
                    if (a != null && verbose) Debug.Log("[Preloader] Touch direct asset: " + a.name);
                    done++; UpdateProgress(done, steps, a != null ? a.name : "(null asset)");
                    if (config.yieldBetweenSteps) yield return null;
                }
            }

            // 2. Resources
            if (config.resources != null)
            {
                for (int i = 0; i < config.resources.Count; i++)
                {
                    var r = config.resources[i];
                    if (string.IsNullOrWhiteSpace(r.resourcePath)) { done++; UpdateProgress(done, steps, "(prázdná cesta)"); continue; }
                    if (r.loadAll)
                    {
                        var loadedAll = Resources.LoadAll<Object>(r.resourcePath);
                        if (verbose) Debug.Log($"[Preloader] LoadAll {r.resourcePath} -> {loadedAll?.Length ?? 0}");
                    }
                    else
                    {
                        var single = Resources.Load<Object>(r.resourcePath);
                        if (verbose) Debug.Log($"[Preloader] Load {r.resourcePath} -> {(single != null ? single.name : "null")}");
                    }
                    done++; UpdateProgress(done, steps, r.resourcePath);
                    if (config.yieldBetweenSteps) yield return null;
                }
            }

            // 3. Pool warmup
            if (config.pools != null && Managers.ObjectPoolManager.HasInstance)
            {
                for (int i = 0; i < config.pools.Count; i++)
                {
                    var p = config.pools[i];
                    if (p.warmCount <= 0 || string.IsNullOrWhiteSpace(p.poolId)) { done++; UpdateProgress(done, steps, p.poolId + " (skip)"); continue; }
                    int spawned = 0;
                    for (int s = 0; s < p.warmCount; s++)
                    {
                        var inst = Managers.ObjectPoolManager.Instance.Spawn(p.poolId, new Vector3(9999f,9999f,0f), Quaternion.identity);
                        if (inst != null)
                        {
                            spawned++;
                            // Return immediately so pool has them available
                            Managers.ObjectPoolManager.Instance.Release(p.poolId, inst);
                        }
                        if (config.yieldBetweenSteps) yield return null;
                    }
                    if (verbose) Debug.Log($"[Preloader] Warmed pool {p.poolId}: {spawned}");
                    done++; UpdateProgress(done, steps, $"Pool {p.poolId}");
                    if (config.yieldBetweenSteps) yield return null;
                }
            }

            // 4. Dokončeno → přepnutí
            UpdateProgress(steps, steps, "Hotovo");
            if (verbose) Debug.Log("[Preloader] Complete.");
            yield return null;

            // Transition
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (verbose) Debug.Log("[Preloader] Loading scene: " + nextSceneName);
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            }
            else if (switchToMainMenuState && GameManager.HasInstance)
            {
                if (verbose) Debug.Log("[Preloader] Switching GameManager state -> MainMenu");
                GameManager.Instance.ReturnToMenu();
            }
        }

        private int CountSteps()
        {
            int steps = 0;
            steps += config.directAssets?.Count ?? 0;
            steps += config.resources?.Count ?? 0;
            steps += config.pools?.Count ?? 0;
            return steps == 0 ? 1 : steps; // avoid div by zero
        }

        private void UpdateProgress(int done, int total, string label)
        {
            float t = total <= 0 ? 1f : Mathf.Clamp01(done / (float)total);
            if (progressSlider != null) progressSlider.value = t;
            if (progressLabel != null) progressLabel.text = $"{Mathf.RoundToInt(t*100f)}% - {label}";
        }
    }
}
