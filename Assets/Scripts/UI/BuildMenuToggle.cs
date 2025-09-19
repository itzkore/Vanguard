using UnityEngine;

namespace BulletHeavenFortressDefense.UI
{
    public class BuildMenuToggle : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.B;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                gameObject.SetActive(!gameObject.activeSelf);
            }
        }
    }
}
