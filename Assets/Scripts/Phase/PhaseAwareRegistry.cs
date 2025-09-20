using System.Collections.Generic;
using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Phase
{
    /// <summary>
    /// Static-style singleton registry for IPhaseAware listeners. Components register in OnEnable and unregister in OnDisable.
    /// WaveManager invokes DispatchPhaseChanged when SetPhase is called.
    /// </summary>
    public class PhaseAwareRegistry : Singleton<PhaseAwareRegistry>
    {
        private readonly List<IPhaseAware> _listeners = new();
        private bool _dirty;

        public static void Register(IPhaseAware listener)
        {
            if (!HasInstance) EnsureRuntimeInstance();
            if (listener == null) return;
            var list = Instance._listeners;
            if (!list.Contains(listener))
            {
                list.Add(listener);
            }
        }

        public static void Unregister(IPhaseAware listener)
        {
            if (!HasInstance || listener == null) return;
            Instance._listeners.Remove(listener);
        }

        public static void DispatchPhaseChanged(BulletHeavenFortressDefense.Managers.WaveManager.WavePhase phase)
        {
            if (!HasInstance) return;
            var list = Instance._listeners;
            // Compact nulls lazily
            bool anyNull = false;
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                if (l == null) { anyNull = true; continue; }
                try { l.OnPhaseChanged(phase); } catch (System.Exception ex) { Debug.LogError("[PhaseAwareRegistry] Listener exception: " + ex); }
            }
            if (anyNull)
            {
                list.RemoveAll(x => x == null);
            }
        }

        private static void EnsureRuntimeInstance()
        {
            var go = new GameObject("PhaseAwareRegistry");
            go.AddComponent<PhaseAwareRegistry>();
            DontDestroyOnLoad(go);
        }
    }
}
