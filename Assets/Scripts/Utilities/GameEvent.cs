using System;
using UnityEngine;

namespace BulletHeavenFortressDefense.Utilities
{
    [CreateAssetMenu(fileName = "GameEvent", menuName = "BHFD/Events/Game Event")]
    public class GameEvent : ScriptableObject
    {
        private event Action _listeners;

        public void Raise()
        {
            _listeners?.Invoke();
        }

        public void Register(Action listener)
        {
            _listeners += listener;
        }

        public void Unregister(Action listener)
        {
            _listeners -= listener;
        }
    }
}
