using System.Collections.Generic;
using UnityEngine;

namespace BulletHeavenFortressDefense.Preload
{
    [CreateAssetMenu(fileName = "PreloadConfig", menuName = "BHFD/Preload Config", order = 50)]
    public class PreloadConfig : ScriptableObject
    {
        [System.Serializable]
        public class ResourceEntry
        {
            [Tooltip("Path under Resources/ (without prefix). E.g. 'TowerData' or 'Projectiles/BulletA'.")] public string resourcePath;
            [Tooltip("If true, load as many assets at this path (Resources.LoadAll)") ] public bool loadAll = true;
        }

        [System.Serializable]
        public class PoolWarmup
        {
            [Tooltip("Pool ID configured v ObjectPoolManager.")] public string poolId;
            [Min(0), Tooltip("Kolik instancí vyžene Preloader skrz Spawn/Release aby se probudily Awake/Start (pokud mají). 0 = skip.")] public int warmCount = 0;
        }

        [Header("Resources to Load")] public List<ResourceEntry> resources = new();
        [Header("Explicit Assets (Addressables ready / direct refs)")] public List<Object> directAssets = new();
        [Header("Pool Warmup")] public List<PoolWarmup> pools = new();
        [Header("Misc")] [Tooltip("Vyčkat frame po každém kroku pro rozfázování (nižší hroty CPU)")] public bool yieldBetweenSteps = true;
    }
}
