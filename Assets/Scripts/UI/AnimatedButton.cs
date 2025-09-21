using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BulletHeavenFortressDefense.UI
{
    [RequireComponent(typeof(Button))]
    public class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private float pulseAmplitude = 0.04f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float lerpSpeed = 12f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1f,1f,1f,0.92f);
        [SerializeField] private Color pressColor = new Color(1f,1f,1f,0.85f);
        [SerializeField] private Graphic targetGraphic; // optional override (Image/Text)

        private Vector3 _baseScale;
        private float _targetScale = 1f;
        private float _pulseTime;
        private Button _button;
        private bool _hovering;
        private bool _pressing;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _baseScale = transform.localScale;
            if (targetGraphic == null)
            {
                targetGraphic = GetComponentInChildren<Graphic>();
            }
        }

        private void Update()
        {
            float pulse = 0f;
            if (_hovering && !_pressing && pulseAmplitude > 0f)
            {
                _pulseTime += Time.unscaledDeltaTime * pulseSpeed;
                pulse = Mathf.Sin(_pulseTime) * pulseAmplitude;
            }
            else
            {
                _pulseTime = 0f;
            }
            float desiredScale = _targetScale + pulse;
            Vector3 goal = _baseScale * desiredScale;
            transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));

            if (targetGraphic != null)
            {
                Color c = normalColor;
                if (_pressing) c = pressColor; else if (_hovering) c = hoverColor;
                targetGraphic.color = Color.Lerp(targetGraphic.color, c, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            _hovering = true; _targetScale = hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false; if (!_pressing) _targetScale = 1f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            _pressing = true; _targetScale = pressScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressing = false; _targetScale = _hovering ? hoverScale : 1f;
        }
    }
}