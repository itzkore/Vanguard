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
                    Debug.LogError($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectile prefab '{prefab.name}' has NO component implementing ITowerProjectile. Add 'Projectile', 'SniperProjectile', 'SlowProjectile' or appropriate script. This is likely why shots fail or prefab appears broken.");
                    issues++;
                }
                else if (verboseAll)
                {
                    Debug.Log($"[ProjectileSanity]{label} OK: Tower '{td.DisplayName}' projectilePrefab '{prefab.name}' has ITowerProjectile ({projectile.GetType().Name}).");
                }

                // Collider2D requirement check (Projectile.cs has [RequireComponent(typeof(Collider2D))]).
                if (prefab.GetComponent<Collider2D>() == null)
                {
                    Debug.LogWarning($"[ProjectileSanity]{label} Tower '{td.DisplayName}' projectile prefab '{prefab.name}' missing Collider2D. It will never hit enemies.");
                    issues++;
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
