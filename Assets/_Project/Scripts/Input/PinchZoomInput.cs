using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Handles multi-touch pinch-to-zoom gestures and forwards them to CameraRig.
    /// Also supports mouse-wheel zoom for editor testing (no pinch on desktop).
    ///
    /// Attach to the same GameObject as CameraRig (or any object that can reach the rig).
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Pinch Detection")]
        [Tooltip("Minimum distance (px) between two touches to be considered a pinch.")]
        [SerializeField] private float minPinchDistance = 50f;

        [Tooltip("Multiplier applied to pinch delta. Higher = more zoom per pinch gesture.")]
        [SerializeField] private float pinchSensitivity = 0.015f;

        [Header("Mouse Wheel (Editor Fallback)")]
        [Tooltip("Multiplier applied to mouse scroll wheel delta.")]
        [SerializeField] private float mouseWheelSensitivity = 1.5f;

        [Header("Smoothing")]
        [Tooltip("If true, zoom changes are smoothed over multiple frames.")]
        [SerializeField] private bool smoothZoom = true;

        [Tooltip("Smoothing factor: 0 = no follow, 1 = instant snap, 0.15 = smooth.")]
        [Range(0f, 1f)] [SerializeField] private float smoothingFactor = 0.15f;

        private CameraRig _cameraRig;

        // Pinch state
        private float _lastPinchDistance;
        private bool _isPinching = false;

        private void Awake()
        {
            _cameraRig = GetComponent<CameraRig>();
        }

        private void Update()
        {
            HandleTouchPinch();
            HandleMouseWheel();
        }

        private void HandleTouchPinch()
        {
            if (UnityEngine.Input.touchCount == 2)
            {
                Touch t0 = UnityEngine.Input.GetTouch(0);
                Touch t1 = UnityEngine.Input.GetTouch(1);

                // Both touches must be in Moved or Stationary phase
                if (t0.phase == TouchPhase.Moved || t0.phase == TouchPhase.Stationary ||
                    t1.phase == TouchPhase.Moved || t1.phase == TouchPhase.Stationary)
                {
                    float currentDistance = Vector2.Distance(t0.position, t1.position);

                    if (!_isPinching)
                    {
                        // Pinch started
                        _lastPinchDistance = currentDistance;
                        _isPinching = true;
                    }
                    else if (currentDistance > minPinchDistance)
                    {
                        // Compute pinch delta: positive = fingers moving apart (zoom out)
                        float delta = currentDistance - _lastPinchDistance;
                        ApplyZoomDelta(delta * pinchSensitivity * 10f);
                        _lastPinchDistance = currentDistance;
                    }
                }
            }
            else
            {
                _isPinching = false;
            }
        }

        private void HandleMouseWheel()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // Positive scroll = zoom in (smaller ortho size)
                ApplyZoomDelta(-scroll * mouseWheelSensitivity * 50f);
            }
#endif
        }

        private void ApplyZoomDelta(float zoomDelta)
        {
            if (_cameraRig == null) return;
            _cameraRig.ApplyPinchZoom(zoomDelta);
        }
    }
}
