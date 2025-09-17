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
            Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, worldCamera.nearClipPlane));
            var contactPosition = new Vector3(worldPoint.x, worldPoint.y, 0f);

            Systems.PlacementSystem.Instance.HandlePrimaryContact(contactPosition);
        }
    }
}
