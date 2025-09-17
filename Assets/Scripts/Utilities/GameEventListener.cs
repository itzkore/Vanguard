using System;
using UnityEngine;

namespace BulletHeavenFortressDefense.Utilities
{
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent response;

        private void OnEnable()
        {
            gameEvent?.Register(OnEventRaised);
        }

        private void OnDisable()
        {
            gameEvent?.Unregister(OnEventRaised);
        }

        private void OnEventRaised()
        {
            response?.Invoke();
        }
    }
}
