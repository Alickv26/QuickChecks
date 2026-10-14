using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Handles multi-touch pinch-to-zoom gestures and forwards them to CameraRig.
    /// Also supports mouse-wheel zoom for editor testing.
    ///
    /// Implementation notes:
    /// - Uses a state machine: Idle -> Pinching -> Idle
    /// - On pinch START: record initial distance, no zoom change
    /// - On pinch MOVE: compute delta from PREVIOUS frame's distance, apply zoom
    /// - On pinch END: stop (don't keep zooming)
    /// - Deadzone: ignore tiny pinch movements (< 5px) to prevent jitter
    /// - Clamps final zoom to InputSettings.minCameraZoom / maxCameraZoom
    ///
    /// Attach to the same GameObject as CameraRig.
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Pinch Detection")]
        [Tooltip("Minimum distance (px) between two touches to be considered a pinch.")]
        [SerializeField] private float minPinchDistance = 50f;

        [Tooltip("Pinch movements smaller than this (px) are ignored to prevent jitter.")]
        [SerializeField] private float pinchDeadzone = 5f;

        [Tooltip("Multiplier applied to pinch delta. Higher = more zoom per pinch gesture. " +
                 "Default 0.02 = ~2 ortho size units per 100px of pinch movement.")]
        [SerializeField] private float pinchSensitivity = 0.02f;

        [Header("Mouse Wheel (Editor Fallback)")]
        [Tooltip("Multiplier applied to mouse scroll wheel delta.")]
        [SerializeField] private float mouseWheelSensitivity = 2f;

        private CameraRig _cameraRig;
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
            // Need exactly 2 touches for a pinch
            if (UnityEngine.Input.touchCount != 2)
            {
                _isPinching = false;
                return;
            }

            Touch t0 = UnityEngine.Input.GetTouch(0);
            Touch t1 = UnityEngine.Input.GetTouch(1);

            float currentDistance = Vector2.Distance(t0.position, t1.position);

            // Not enough distance yet — wait
            if (currentDistance < minPinchDistance)
            {
                _isPinching = false;
                return;
            }

            // If either touch just began, this is the START of a pinch
            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || !_isPinching)
            {
                _lastPinchDistance = currentDistance;
                _isPinching = true;
                return;  // No zoom change on the first frame
            }

            // Pinch in progress — compute delta from last frame
            if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                float delta = currentDistance - _lastPinchDistance;

                // Apply deadzone — ignore tiny movements
                if (Mathf.Abs(delta) < pinchDeadzone)
                {
                    return;  // Don't update _lastPinchDistance, wait for bigger movement
                }

                // delta > 0: fingers moving apart (zoom OUT, increase ortho size)
                // delta < 0: fingers moving together (zoom IN, decrease ortho size)
                float zoomDelta = delta * pinchSensitivity;
                ApplyZoomDelta(zoomDelta);

                // Update last distance for next frame's delta computation
                _lastPinchDistance = currentDistance;
            }
            else if (t0.phase == TouchPhase.Ended || t1.phase == TouchPhase.Ended ||
                     t0.phase == TouchPhase.Canceled || t1.phase == TouchPhase.Canceled)
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
                // Positive scroll = wheel up = zoom IN (ortho size decreases)
                ApplyZoomDelta(-scroll * mouseWheelSensitivity);
            }
#endif
        }

        private void ApplyZoomDelta(float zoomDelta)
        {
            if (_cameraRig == null)
            {
                _cameraRig = GetComponent<CameraRig>();
                if (_cameraRig == null) return;
            }
            _cameraRig.ApplyPinchZoom(zoomDelta);
        }
    }
}
