using System.Text;
using UnityEngine;
using BulletHeavenFortressDefense.Managers;
using BulletHeavenFortressDefense.Data;
using BulletHeavenFortressDefense.Entities;

namespace BulletHeavenFortressDefense.Diagnostics
{
    /// <summary>
    /// Runtime validator that inspects every unlocked tower's projectile prefab and reports
    /// missing or suspicious configuration (e.g. no ITowerProjectile implementation, no Collider2D, disabled object).
    /// This helps surface issues like a prefab that lost its MonoBehaviour script reference after a GUID / meta change.
    /// Safe to leave in builds (only logs warnings/errors once). Remove or disable verbose if noisy.
    /// </summary>
    [DefaultExecutionOrder(-25)] // Run early, after TowerManager (- default 0) Awake has executed.
    public class ProjectilePrefabSanityCheck : MonoBehaviour
    {
        [Tooltip("If true, prints an info line for every projectile prefab even when valid.")] public bool verboseAll = false;
        [Tooltip("If true, runs again after a short delay to catch late-discovered towers (e.g. via Resources rescan)." )]
        public bool secondPass = true;
        [Tooltip("Delay (seconds) before optional second pass.")] public float secondPassDelay = 2f;
        [Header("Auto Repair")]
        [Tooltip("If true, when a projectile prefab has no ITowerProjectile component, a basic 'Projectile' component and Collider2D will be added at runtime (not saved to asset). Helps keep gameplay working after missing script GUID issues.")] public bool autoAddBasicProjectile = true;
        [Tooltip("Log a message when an auto repair is performed.")] public bool logRepairs = true;

        private bool _ranOnce = false;

        private void Start()
        {
            RunValidation();
            if (secondPass) { Invoke(nameof(RunValidationSecondPass), secondPassDelay); }
        }

        private void RunValidationSecondPass()
        {
            // Avoid duplicate log spam if no new towers appeared.
            RunValidation(label:"(SecondPass)");
        }

        private void RunValidation(string label = "")
        {
            if (!TowerManager.HasInstance)
            {
                Debug.LogWarning($"[ProjectileSanity]{label} TowerManager not yet available – skipping.");
                return;
            }

            var towers = TowerManager.Instance.UnlockedTowers;
            if (towers == null || towers.Count == 0)
            {
                Debug.LogWarning($"[ProjectileSanity]{label} No unlocked towers to validate.");
                return;
            }

            int issues = 0;
            for (int i = 0; i < towers.Count; i++)
            {
                var td = towers[i];
                if (td == null)
                {
                    Debug.LogWarning($"[ProjectileSanity]{label} Tower index {i} is <null>.");
                    issues++;
                    continue;
                }

                var prefab = td.ProjectilePrefab;
                if (prefab == null)
                {
                    Debug.LogError($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectilePrefab is NULL.");
                    issues++;
                    continue;
                }

                // Prefab root disabled?
                if (!prefab.activeSelf)
                {
                    Debug.LogWarning($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectile prefab '{prefab.name}' is inactive at root (may prevent pooling logic using activeSelf checks).");
                }

                // Find a component implementing ITowerProjectile on the root or children.
                ITowerProjectile projectile = prefab.GetComponent<ITowerProjectile>();
                if (projectile == null)
                {
                    projectile = prefab.GetComponentInChildren<ITowerProjectile>(true);
                }

                if (projectile == null)
                {
                    if (autoAddBasicProjectile)
                    {
                        // Attempt automatic repair: add base Projectile component
                        var repaired = prefab.AddComponent<Projectile>();
                        projectile = repaired;
                        if (logRepairs)
                        {
                            Debug.LogWarning($"[ProjectileSanity]{label} AUTO-REPAIR: Added basic Projectile component to '{prefab.name}' (Tower '{td.DisplayName}') to restore functionality. Please save prefab in editor if permanent.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectile prefab '{prefab.name}' has NO component implementing ITowerProjectile. Add 'Projectile', 'SniperProjectile', 'SlowProjectile' or appropriate script. This is likely why shots fail or prefab appears broken.");
                        issues++;
                    }
                }
                if (projectile != null && verboseAll)
                {
                    Debug.Log($"[ProjectileSanity]{label} OK: Tower '{td.DisplayName}' projectilePrefab '{prefab.name}' has ITowerProjectile ({projectile.GetType().Name}).");
                }

                // Collider2D requirement check (Projectile.cs has [RequireComponent(typeof(Collider2D))]).
                var col = prefab.GetComponent<Collider2D>();
                if (col == null)
                {
                    if (autoAddBasicProjectile)
                    {
                        col = prefab.AddComponent<CircleCollider2D>();
                        col.isTrigger = true;
                        if (logRepairs)
                        {
                            Debug.LogWarning($"[ProjectileSanity]{label} AUTO-REPAIR: Added CircleCollider2D to '{prefab.name}' (Tower '{td.DisplayName}'). Configure radius in prefab to fine-tune.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectile prefab '{prefab.name}' missing Collider2D. It will never hit enemies.");
                        issues++;
                    }
                }

                // Rigidbody2D optional – could help with future physics; only informatively warn if absent.
                if (prefab.GetComponent<Rigidbody2D>() == null && verboseAll)
                {
                    Debug.Log($"[ProjectileSanity]{label} Info: '{prefab.name}' has no Rigidbody2D (fine for manual transform movement).");
                }
            }

            if (issues == 0)
            {
                if (verboseAll || !_ranOnce)
                {
                    Debug.Log($"[ProjectileSanity]{label} All projectile prefabs passed basic validation.");
                }
            }
            else
            {
                Debug.LogWarning($"[ProjectileSanity]{label} Validation found {issues} issue(s). See logs above for details.");
            }

            _ranOnce = true;
        }
    }
}
