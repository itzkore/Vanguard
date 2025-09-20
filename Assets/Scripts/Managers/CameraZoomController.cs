using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BulletHeavenFortressDefense.Managers
{
    [DisallowMultipleComponent]
    public class CameraZoomController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [Header("Zoom Range")]
    [SerializeField, Min(0.01f)] private float minOrthoSize = 2.5f;
    [SerializeField, Min(0.01f), Tooltip("Maximum orthographic size (zoomed out). Can exceed default start size to view larger battle area.")] private float maxOrthoSize = 18f;
    [SerializeField, Tooltip("If true, when scene starts the default camera size becomes the midpoint between min & max if current size is outside.")] private bool autoNormalizeAtStart = true;
    [Header("World Bounds (Optional)")]
    [SerializeField, Tooltip("If set, camera panning/zoom re-centering will be clamped so that the view never leaves these world bounds. Values represent the full rectangle (minX, maxX, minY, maxY). Leave max <= min to auto-use baseline extents.")] private float boundsMinX = 0f;
    [SerializeField] private float boundsMaxX = -1f;
    [SerializeField] private float boundsMinY = 0f;
    [SerializeField] private float boundsMaxY = -1f;
    [SerializeField, Tooltip("Extra padding inside bounds (world units) so camera doesn't hug the hard edge.")] private float innerPadding = 0.25f;
        [Header("Zoom Speeds")]
        [SerializeField] private float wheelZoomSpeed = 0.75f; // units per wheel notch
        [SerializeField] private float pinchZoomSpeed = 0.01f; // units per pixel delta between touches
        [Header("UX")]
        [SerializeField, Tooltip("Ignore mouse wheel zoom when pointer is over UI.")] private bool blockZoomWhenPointerOverUI = true;
        [SerializeField, Tooltip("Smoothly interpolate camera size to target.")] private float smoothing = 12f;
    [Header("Panning")]
    [SerializeField, Tooltip("Enable panning when zoomed in.")] private bool enablePanning = true;
    [SerializeField, Tooltip("Block panning when pointer is over UI.")] private bool blockPanningWhenPointerOverUI = false;
    [SerializeField, Tooltip("Panning multiplier (1 = 1 world unit per world unit/pixel factor)." )] private float panSpeedPerPixel = 1.0f;
    [SerializeField, Tooltip("Smooth damping for camera position.")] private float panSmoothing = 18f;
    // Removed unused panBorderExtra (previously intended for overscroll) to eliminate CS0414 warning.

    private float _targetSize;
    private float _defaultSize;
    private Vector3 _targetPos;
        private Vector3 _panVelocity;
        private bool _dragging;
        private Vector2 _prevPointer;
    private Vector3 _baselineCenter;
        private bool _pinching;
        private float _prevPinchDistance;
        // Accumulated pan offset at max zoom-in level (scaled down as we zoom out so we always recenter at default size)
        private Vector3 _panOffset;

        public float DefaultSize => _defaultSize;
        public float MaxOrthoSize => maxOrthoSize;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            if (targetCamera != null)
            {
                if (!targetCamera.orthographic)
                {
                    targetCamera.orthographic = true;
                }
                _defaultSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
                // Allow zooming further out than initial default; ensure ordering
                maxOrthoSize = Mathf.Max(minOrthoSize + 0.01f, maxOrthoSize);
                if (autoNormalizeAtStart)
                {
                    // If starting size is outside range, pull it toward midpoint for a balanced initial view
                    float mid = (minOrthoSize + maxOrthoSize) * 0.5f;
                    if (targetCamera.orthographicSize < minOrthoSize || targetCamera.orthographicSize > maxOrthoSize)
                    {
                        targetCamera.orthographicSize = Mathf.Clamp(mid, minOrthoSize, maxOrthoSize);
                    }
                }
                _targetSize = Mathf.Clamp(targetCamera.orthographicSize, minOrthoSize, maxOrthoSize);
                targetCamera.orthographicSize = _targetSize;
                _targetPos = targetCamera.transform.position;
                _baselineCenter = _targetPos;
                _panOffset = Vector3.zero;
            }
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                return;
            }

            float size = _targetSize;

            // Mouse wheel (Input System if available, fallback to legacy)
            float wheel = 0f;
            if (Mouse.current != null)
            {
                // Typically positive when scrolling up, negative when down
                wheel = Mouse.current.scroll.ReadValue().y / 120f; // normalize notches (~120 per notch on Windows)
            }
            else
            {
                wheel = Input.mouseScrollDelta.y;
            }

            if (Mathf.Abs(wheel) > 0.0001f)
            {
                if (!blockZoomWhenPointerOverUI || !IsPointerOverUI())
                {
                    size -= wheel * wheelZoomSpeed;
                }
            }

            // Pinch zoom using Input System touch if available; fallback to legacy if needed
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                int active = 0;
                Vector2 p0 = default, p1 = default;
                foreach (var t in touchscreen.touches)
                {
                    if (t.press.isPressed)
                    {
                        if (active == 0) p0 = t.position.ReadValue();
                        else if (active == 1) p1 = t.position.ReadValue();
                        active++;
                        if (active >= 2) break;
                    }
                }
                if (active >= 2)
                {
                    float dist = Vector2.Distance(p0, p1);
                    if (_pinching)
                    {
                        float delta = dist - _prevPinchDistance;
                        size -= delta * pinchZoomSpeed;
                    }
                    _prevPinchDistance = dist;
                    _pinching = true;
                }
                else
                {
                    _pinching = false;
                }
            }
            else
            {
                // Legacy pinch (if old input is enabled)
                if (Input.touchCount >= 2)
                {
                    var t0 = Input.GetTouch(0);
                    var t1 = Input.GetTouch(1);
                    float dist = Vector2.Distance(t0.position, t1.position);
                    if (_pinching)
                    {
                        float delta = dist - _prevPinchDistance;
                        size -= delta * pinchZoomSpeed;
                    }
                    _prevPinchDistance = dist;
                    _pinching = true;
                }
                else
                {
                    _pinching = false;
                }
            }

            // Clamp to configured min/max (now supports extended zoom out) 
            size = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
            _targetSize = size;

            // Smooth toward target size
            float k = 1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.unscaledDeltaTime);
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, _targetSize, k);

            // Zoom factor relative to current min->max range (0 = max zoom out, 1 = max zoom in)
            float zoomFactor = Mathf.InverseLerp(maxOrthoSize, minOrthoSize, _targetSize);

            // Handle panning only while zoomed in (some margin to avoid tiny jitter near default)
            if (enablePanning && _targetSize < _defaultSize - 0.0001f)
            {
                HandlePanning();
            }
            else
            {
                _dragging = false;
            }

            // Calculate rightward center shift to maintain left boundary rule
            Vector3 adjustedCenter = CalculateZoomAdjustedCenter(_targetSize);

            // Recompute target position from pan offset scaled by zoomFactor
            if (Mathf.Approximately(_targetSize, maxOrthoSize) || zoomFactor <= 0.001f) // fully zoomed out -> recenter
            {
                _panOffset = Vector3.zero; // clear stored offset
                _targetPos = adjustedCenter;
                targetCamera.transform.position = adjustedCenter; // instant snap
            }
            else
            {
                _targetPos = adjustedCenter + _panOffset * zoomFactor;
                _targetPos = ClampToPanBounds(_targetPos);
                // Smooth move toward target position
                float kp = 1f - Mathf.Exp(-Mathf.Max(0f, panSmoothing) * Time.unscaledDeltaTime);
                targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, _targetPos, kp);
            }
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        private void HandlePanning()
        {
            if (blockPanningWhenPointerOverUI && IsPointerOverUI())
            {
                _dragging = false;
                return;
            }

            bool moved = false;
            Vector2 curPointer = Vector2.zero;

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed)
                {
                    curPointer = Mouse.current.position.ReadValue();
                    if (!_dragging)
                    {
                        _dragging = true;
                        _prevPointer = curPointer;
                        return;
                    }
                    Vector2 delta = curPointer - _prevPointer;
                    _prevPointer = curPointer;
                    ApplyPanDelta(delta);
                    moved = true;
                }
                else
                {
                    _dragging = false;
                }
            }
            else
            {
                // Legacy input fallback
                if (Input.GetMouseButton(0))
                {
                    curPointer = (Vector2)Input.mousePosition;
                    if (!_dragging)
                    {
                        _dragging = true;
                        _prevPointer = curPointer;
                        return;
                    }
                    Vector2 delta = curPointer - _prevPointer;
                    _prevPointer = curPointer;
                    ApplyPanDelta(delta);
                    moved = true;
                }
                else
                {
                    _dragging = false;
                }
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                int downCount = 0; TouchControl first = null;
                foreach (var t in touchscreen.touches)
                {
                    if (t.press.isPressed)
                    {
                        if (downCount == 0) first = t;
                        downCount++;
                        if (downCount > 1) break;
                    }
                }
                if (downCount == 1 && !_pinching)
                {
                    curPointer = first.position.ReadValue();
                    if (!_dragging)
                    {
                        _dragging = true;
                        _prevPointer = curPointer;
                        return;
                    }
                    Vector2 delta = curPointer - _prevPointer;
                    _prevPointer = curPointer;
                    ApplyPanDelta(delta);
                    moved = true;
                }
                else if (downCount == 0)
                {
                    _dragging = false;
                }
            }

            if (!moved) return;
        }

        private void ApplyPanDelta(Vector2 screenDelta)
        {
            if (screenDelta.sqrMagnitude <= 0f) return;
            float unitsPerPixel = (targetCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
            Vector2 worldDelta = -screenDelta * unitsPerPixel * Mathf.Max(0.0001f, panSpeedPerPixel);
            _panOffset += new Vector3(worldDelta.x, worldDelta.y, 0f);
        }

        private Vector3 ClampToPanBounds(Vector3 desired)
        {
            float z = targetCamera.transform.position.z;
            // Default viewport extents
            float halfHDefault = _defaultSize;
            float halfWDefault = halfHDefault * targetCamera.aspect;
            // Current viewport extents (zoomed-in only)
            float halfHCurrent = Mathf.Min(targetCamera.orthographicSize, _defaultSize);
            float halfWCurrent = halfHCurrent * targetCamera.aspect;

            // Keep the entire current view inside the default viewport: clamp camera center so edges don't cross
            Vector3 center = new Vector3(_baselineCenter.x, _baselineCenter.y, z);
            float minX = center.x - (halfWDefault - halfWCurrent);
            float maxX = center.x + (halfWDefault - halfWCurrent);
            float minY = center.y - (halfHDefault - halfHCurrent);
            float maxY = center.y + (halfHDefault - halfHCurrent);

            // If user provided explicit bounds (valid rectangle), override with those
            if (HasCustomBounds())
            {
                // Bounds represent allowed world area for visible content, so clamp camera center such that edges stay inside
                float bMinX = Mathf.Min(boundsMinX, boundsMaxX);
                float bMaxX = Mathf.Max(boundsMinX, boundsMaxX);
                float bMinY = Mathf.Min(boundsMinY, boundsMaxY);
                float bMaxY = Mathf.Max(boundsMinY, boundsMaxY);
                // Apply inner padding
                bMinX += innerPadding; bMaxX -= innerPadding; bMinY += innerPadding; bMaxY -= innerPadding;
                // Shrink allowed center range by current half extents
                minX = bMinX + halfWCurrent;
                maxX = bMaxX - halfWCurrent;
                minY = bMinY + halfHCurrent;
                maxY = bMaxY - halfHCurrent;
                if (minX > maxX) // bounds too small for this zoom; collapse to midpoint
                {
                    float midX = (bMinX + bMaxX) * 0.5f; minX = maxX = midX;
                }
                if (minY > maxY)
                {
                    float midY = (bMinY + bMaxY) * 0.5f; minY = maxY = midY;
                }
            }

            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
            desired.z = z;
            return desired;
        }

        private bool HasCustomBounds()
        {
            return boundsMaxX > boundsMinX && boundsMaxY > boundsMinY; // valid rectangle supplied
        }

        /// <summary>
        /// Calculate camera center adjusted for zoom level to maintain left boundary rule.
        /// As we zoom out, shift center rightward so fortress left side stays at screen left edge.
        /// </summary>
        private Vector3 CalculateZoomAdjustedCenter(float currentOrthoSize)
        {
            // When zooming out from default size, we need to shift camera right 
            // so that the left edge of the viewport stays at the same world position
            
            // At default size, left edge of viewport is at: _baselineCenter.x - (defaultHalfWidth)
            // At current size, left edge would be at: adjustedCenter.x - (currentHalfWidth)
            // We want these to be equal, so: adjustedCenter.x = _baselineCenter.x + (currentHalfWidth - defaultHalfWidth)
            
            float defaultHalfWidth = _defaultSize * targetCamera.aspect;
            float currentHalfWidth = currentOrthoSize * targetCamera.aspect;
            float rightwardShift = currentHalfWidth - defaultHalfWidth;
            
            Vector3 adjustedCenter = _baselineCenter;
            adjustedCenter.x += rightwardShift;
            
            return adjustedCenter;
        }
    }
}
