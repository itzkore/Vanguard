using UnityEngine;
using UnityEngine.UI;

namespace BulletHeavenFortressDefense.UI
{
    // Ensures status panel texts don't get culled / disabled during rapid resolution changes.
    // (Occasional Unity quirk when dynamically created UI + CanvasScaler recalculates.)
    public class StatusPanelKeepAlive : MonoBehaviour
    {
        private Text[] _texts;
        private RectTransform _rt;
        private int _frameCounter;

        private void Awake()
        {
            _texts = GetComponentsInChildren<Text>(true);
            _rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandlePreRender;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandlePreRender;
        }

        private void HandlePreRender()
        {
            if (_texts == null) return;
            // Periodically (not every frame) ensure they stay enabled & not culled
            _frameCounter++;
            if (_frameCounter % 10 != 0) return;
            foreach (var t in _texts)
            {
                if (t != null && !t.enabled) t.enabled = true;
                var cr = t != null ? t.GetComponent<CanvasRenderer>() : null;
                if (cr != null && cr.cull) cr.cull = false;
            }
            // Keep panel on top just in case sibling reordering happens on resize
            _rt?.SetAsLastSibling();
        }
    }
}