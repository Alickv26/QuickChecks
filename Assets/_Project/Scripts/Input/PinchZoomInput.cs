using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Map-style pan and zoom for the camera. Works WITH the CameraRig follow,
    /// not against it.
    ///
    /// Design:
    ///   - Two-finger PINCH: zoom in/out (directly sets ortho size, no lerp conflict)
    ///   - Two-finger PAN: move camera (offset from follow target, not raw position)
    ///   - Pan creates a persistent offset that the follow camera respects
    ///   - After pan, follow resumes but with the offset applied (so the camera
    ///     doesn't snap back to the kart — it stays where the player panned, relative
    ///     to the follow target)
    ///   - Zoom and pan work simultaneously (fingers can pinch while drifting)
    ///
    /// Pan offset is cleared when the player swipes (returns to follow-the-kart).
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Zoom")]
        [Tooltip("How much a pinch gesture affects zoom. Higher = more zoom per pinch pixel.")]
        [SerializeField] private float zoomSensitivity = 0.015f;

        [Header("Pan")]
        [Tooltip("How much a two-finger drag affects camera pan.")]
        [SerializeField] private float panSensitivity = 1.0f;
        [Tooltip("After panning, how long before the follow camera re-centers on the kart (seconds).")]
        [SerializeField] private float panRecenterDelay = 3f;

        [Header("Mouse (Editor)")]
        [SerializeField] private float mouseWheelZoomSpeed = 2f;

        private CameraRig _cameraRig;
        private UnityEngine.Camera _camera;

        // Gesture state
        private float _lastPinchDistance;
        private Vector2 _lastMidpoint;
        private bool _gestureActive = false;
        private float _gestureStartTime;

        // Pan offset — added on top of the follow camera's target position
        private Vector3 _panOffset = Vector3.zero;
        private float _panRecenterTimer = 0f;

        // Track whether we're in a two-finger gesture this frame
        public static bool IsTwoFingerGestureActive { get; private set; }

        private void Awake()
        {
            _cameraRig = GetComponent<CameraRig>();
            _camera = GetComponent<UnityEngine.Camera>();
        }

        private void Update()
        {
            HandleTouchGesture();
            HandleMouseWheel();

            // Recenter: gradually move panOffset back to zero after panRecenterDelay
            if (_panRecenterTimer > 0f)
            {
                _panRecenterTimer -= Time.deltaTime;
                if (_panRecenterTimer <= 0f)
                {
                    // Smoothly lerp panOffset to zero over 1 second
                    _panOffset = Vector3.Lerp(_panOffset, Vector3.zero, Time.deltaTime * 2f);
                    if (_panOffset.magnitude < 0.1f)
                    {
                        _panOffset = Vector3.zero;
                    }
                }
            }

            // Apply pan offset to the camera (on top of whatever CameraRig set)
            if (_panOffset != Vector3.zero)
            {
                _camera.transform.position += _panOffset;
            }
        }

        private void HandleTouchGesture()
        {
            if (UnityEngine.Input.touchCount < 2)
            {
                _gestureActive = false;
                IsTwoFingerGestureActive = false;
                return;
            }

            IsTwoFingerGestureActive = true;

            Touch t0 = UnityEngine.Input.GetTouch(0);
            Touch t1 = UnityEngine.Input.GetTouch(1);

            float currentDistance = Vector2.Distance(t0.position, t1.position);
            Vector2 currentMidpoint = (t0.position + t1.position) * 0.5f;

            if (!_gestureActive)
            {
                _lastPinchDistance = currentDistance;
                _lastMidpoint = currentMidpoint;
                _gestureActive = true;
                _gestureStartTime = Time.time;
                return;
            }

            // Compute deltas
            float pinchDelta = currentDistance - _lastPinchDistance;
            Vector2 panDelta = currentMidpoint - _lastMidpoint;

            // ALWAYS apply both pinch and pan — players naturally do both at once
            // (fingers drift while pinching). Small values are fine.

            // PINCH (zoom): fingers apart → zoom in, fingers together → zoom out
            if (Mathf.Abs(pinchDelta) > 0.5f)
            {
                float zoomChange = -pinchDelta * zoomSensitivity;
                ApplyZoom(zoomChange);
            }

            // PAN: both fingers move in same direction
            if (panDelta.magnitude > 1f)
            {
                ApplyPan(panDelta);
            }

            _lastPinchDistance = currentDistance;
            _lastMidpoint = currentMidpoint;
        }

        private void ApplyZoom(float zoomDelta)
        {
            if (_cameraRig == null) return;
            _cameraRig.ApplyPinchZoom(zoomDelta);
        }

        private void ApplyPan(Vector2 screenDelta)
        {
            if (_camera == null) return;

            float worldUnitsPerScreenPx = _camera.orthographicSize * 2f / Screen.height;
            Vector3 worldDelta = new Vector3(
                -screenDelta.x * worldUnitsPerScreenPx * panSensitivity,
                -screenDelta.y * worldUnitsPerScreenPx * panSensitivity,
                0
            );

            // Add to pan offset (not raw position) so the follow camera respects it
            _panOffset += worldDelta;
            _panRecenterTimer = panRecenterDelay;
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

        /// <summary>Clears the pan offset — called when a swipe fires (return to follow-the-kart).</summary>
        public static void ClearPanOffset()
        {
            // Instance-level clear since static can't access instance fields
            if (Instance != null)
            {
                Instance._panOffset = Vector3.zero;
                Instance._panRecenterTimer = 0f;
            }
        }

        private static PinchZoomInput Instance;
        private void OnEnable() { Instance = this; }
    }
}
