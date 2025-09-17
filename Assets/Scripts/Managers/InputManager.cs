using UnityEngine;
using UnityEngine.InputSystem;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Managers
{
    public class InputManager : Singleton<InputManager>
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private InputActionReference primaryContactAction;
        [SerializeField] private InputActionReference pointAction;

        protected override void Awake()
        {
            base.Awake();

            if (primaryContactAction != null)
            {
                primaryContactAction.action.performed += OnPrimaryContact;
            }
        }

        private void OnEnable()
        {
            primaryContactAction?.action.Enable();
            pointAction?.action.Enable();
        }

        private void OnDisable()
        {
            if (primaryContactAction != null)
            {
                primaryContactAction.action.performed -= OnPrimaryContact;
                primaryContactAction.action.Disable();
            }

            pointAction?.action.Disable();
        }

        private void OnPrimaryContact(InputAction.CallbackContext context)
        {
            if (worldCamera == null || pointAction == null)
            {
                return;
            }

            Vector2 screenPos = pointAction.action.ReadValue<Vector2>();
            var ray = worldCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit))
            {
                Debug.Log($"Primary contact at {hit.point}");
                Systems.PlacementSystem.Instance.HandlePrimaryContact(hit.point);
            }
        }
    }
}
