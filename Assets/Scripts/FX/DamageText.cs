using UnityEngine;
using TMPro;

namespace BulletHeavenFortressDefense.FX
{
    // Handles a single floating damage text instance (rise + fade then return to pool)
    public class DamageText : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private Vector3 velocity = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0,1,1,0);
        [SerializeField] private Gradient normalColor;
        [SerializeField] private Gradient critColor;
        [SerializeField] private float randomJitter = 0.2f;

        private float _timer;
        private DamageTextManager _manager;
        private Vector3 _startPos;

        private void Awake()
        {
            if (text == null)
            {
                text = GetComponentInChildren<TMP_Text>();
            }
            if (normalColor == null || normalColor.colorKeys.Length == 0)
            {
                normalColor = new Gradient();
                normalColor.SetKeys(
                    new GradientColorKey[]{ new GradientColorKey(Color.white,0f), new GradientColorKey(new Color(1f,0.95f,0.6f),1f)},
                    new GradientAlphaKey[]{ new GradientAlphaKey(1f,0f), new GradientAlphaKey(1f,1f)}
                );
            }
            if (critColor == null || critColor.colorKeys.Length == 0)
            {
                critColor = new Gradient();
                critColor.SetKeys(
                    new GradientColorKey[]{ new GradientColorKey(new Color(1f,0.65f,0.2f),0f), new GradientColorKey(Color.red,1f)},
                    new GradientAlphaKey[]{ new GradientAlphaKey(1f,0f), new GradientAlphaKey(1f,1f)}
                );
            }
        }

        public void SetManager(DamageTextManager mgr) => _manager = mgr;

        public void Show(Vector3 worldPos, float amount, bool crit)
        {
            _timer = lifetime;
            _startPos = worldPos + new Vector3(Random.Range(-randomJitter, randomJitter), Random.Range(-randomJitter, randomJitter), 0f);
            transform.position = _startPos;
            if (text != null)
            {
                text.text = Mathf.RoundToInt(amount).ToString();
                text.color = (crit ? critColor.Evaluate(0f) : normalColor.Evaluate(0f));
            }
        }

        private void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.deltaTime;
            float tNorm = 1f - Mathf.Clamp01(_timer / lifetime);
            transform.position = _startPos + velocity * tNorm;

            if (text != null)
            {
                // Evaluate gradient across life
                Gradient grad = (text.color == critColor.Evaluate(0f) || text.color == critColor.Evaluate(1f)) ? critColor : normalColor;
                text.color = grad.Evaluate(tNorm);
                var c = text.color;
                c.a = alphaCurve.Evaluate(tNorm);
                text.color = c;
            }

            if (_timer <= 0f)
            {
                _manager?.Release(this);
            }
        }
    }
}
