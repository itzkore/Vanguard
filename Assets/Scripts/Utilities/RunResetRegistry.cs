using System.Collections.Generic;
namespace BulletHeavenFortressDefense.Utilities
{
    /// <summary>
    /// Lightweight static registry for systems that need a deterministic per-run reset without broad scene scanning.
    /// Components implementing IRunResettable should call Register on OnEnable and Unregister on OnDisable.
    /// </summary>
    public static class RunResetRegistry
    {
        private static readonly List<IRunResettable> _items = new();
        public static void Register(IRunResettable item)
        {
            if (item == null) return; if (_items.Contains(item)) return; _items.Add(item);
        }
        public static void Unregister(IRunResettable item)
        {
            if (item == null) return; _items.Remove(item);
        }
        public static void ResetAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i]; if (it == null) continue;
                try { it.ResetForNewRun(); } catch (System.Exception ex) { UnityEngine.Debug.LogError("[RunResetRegistry] Exception: " + ex); }
            }
        }
    }
}
