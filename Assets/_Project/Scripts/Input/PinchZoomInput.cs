using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Map-style pan and zoom for the camera.
    ///
    /// GESTURES:
    /// - One finger flick = swipe (handled by SwipeDetector, not here)
    /// - Two finger pinch = zoom in/out
    /// - Two finger drag (both fingers move same direction) = pan camera
    ///
    /// When 2+ fingers are down, SwipeDetector releases its touch so this
    /// component can handle the multi-touch gesture.
    ///
    /// Pan temporarily overrides the camera follow — camera moves freely.
    /// When fingers lift, camera returns to following the kart after a short delay.
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Zoom")]
        [Tooltip("Multiplier for pinch delta. Higher = more zoom per pinch.")]
        [SerializeField] private float zoomSensitivity = 0.02f;

        [Tooltip("Minimum distance between fingers to start pinch.")]
        [SerializeField] private float minPinchDistance = 40f;

        [Header("Pan")]
        [Tooltip("Multiplier for pan delta. Higher = more pan per drag.")]
        [SerializeField] private float panSensitivity = 0.01f;

        [Tooltip("How long after pan ends before camera returns to following kart (seconds).")]
        [SerializeField] private float panLingerSec = 2f;

        [Header("Mouse (Editor)")]
        [SerializeField] private float mouseWheelZoomSpeed = 3f;

        private CameraRig _cameraRig;
        private UnityEngine.Camera _camera;

        // Pinch state
        private float _lastPinchDistance;
        private bool _isPinching = false;

        // Pan state
        private Vector2 _lastPanMidpoint;
        private bool _isPanning = false;
        private float _panLingerTimer = 0f;

        private void Awake()
        {
            _cameraRig = GetComponent<CameraRig>();
            _camera = GetComponent<UnityEngine.Camera>();
        }

        private void Update()
        {
            HandleTouchGesture();
            HandleMouseWheel();

            // Countdown pan linger timer
            if (_panLingerTimer > 0f)
            {
                _panLingerTimer -= Time.deltaTime;
                if (_panLingerTimer <= 0f && _cameraRig != null)
                {
                    // Re-enable camera follow
                    _cameraRig.enabled = true;
                    Debug.Log("[PinchZoom] Pan linger ended — camera following kart again.");
                }
            }
        }

        private void HandleTouchGesture()
        {
            if (UnityEngine.Input.touchCount < 2)
            {
                _isPinching = false;
                _isPanning = false;
                return;
            }

            Touch t0 = UnityEngine.Input.GetTouch(0);
            Touch t1 = UnityEngine.Input.GetTouch(1);

            Vector2 currentMidpoint = (t0.position + t1.position) * 0.5f;
            float currentDistance = Vector2.Distance(t0.position, t1.position);

            // Start gesture
            if (!_isPinching && !_isPanning)
            {
                _lastPinchDistance = currentDistance;
                _lastPanMidpoint = currentMidpoint;
                _isPinching = currentDistance > minPinchDistance;
                _isPanning = true;

                // Disable camera follow while panning
                if (_cameraRig != null) _cameraRig.enabled = false;
                return;
            }

            // ZOOM: detect pinch (distance between fingers changing)
            if (currentDistance > minPinchDistance)
            {
                float distanceDelta = currentDistance - _lastPinchDistance;
                if (Mathf.Abs(distanceDelta) > 2f)
                {
                    // Positive delta = fingers apart = zoom out (increase ortho size)
                    // Negative delta = fingers together = zoom in (decrease ortho size)
                    float zoomDelta = distanceDelta * zoomSensitivity;
                    ApplyZoom(zoomDelta);
                    _lastPinchDistance = currentDistance;
                }
            }

            // PAN: detect both fingers moving in same direction
            Vector2 panDelta = currentMidpoint - _lastPanMidpoint;
            if (panDelta.magnitude > 2f)
            {
                ApplyPan(panDelta);
                _lastPanMidpoint = currentMidpoint;
            }

            // Set linger timer so camera stays where user panned for a bit
            _panLingerTimer = panLingerSec;
        }

        private void ApplyZoom(float zoomDelta)
        {
            if (_cameraRig == null) return;
            _cameraRig.ApplyPinchZoom(zoomDelta);
        }

        private void ApplyPan(Vector2 screenDelta)
        {
            if (_camera == null) return;

            // Convert screen-space pan delta to world-space movement
            // Pan direction is inverted (like dragging a map — drag right = world moves left)
            float worldUnitsPerScreenPx = _camera.orthographicSize * 2f / Screen.height;
            Vector3 worldDelta = new Vector3(
                -screenDelta.x * worldUnitsPerScreenPx * panSensitivity * 100f,
                -screenDelta.y * worldUnitsPerScreenPx * panSensitivity * 100f,
                0
            );

            _camera.transform.position += worldDelta;
        }

        private void HandleMouseWheel()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                ApplyZoom(-scroll * mouseWheelZoomSpeed);
            }
#endif
        }
    }
}
