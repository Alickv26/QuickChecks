using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Map-style pan and zoom for the camera.
    ///
    /// Two-finger gestures:
    /// - PINCH: fingers move apart = zoom IN (see things bigger = decrease ortho size)
    ///          fingers move together = zoom OUT (see more = increase ortho size)
    /// - PAN: both fingers move in the same direction = move camera
    ///
    /// The gesture is classified each frame based on the ratio of:
    ///   pinch_delta (change in distance between fingers)
    ///   pan_delta (movement of the midpoint)
    ///
    /// If pinch_delta > pan_delta → it's a zoom
    /// If pan_delta > pinch_delta → it's a pan
    /// This prevents the random zoom that happens when panning with slightly uneven fingers.
    ///
    /// IMPORTANT: two-finger gestures are detected FIRST and suppress single-finger
    /// input (swipes). This prevents accidental kart swipes while pinching/panning.
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Zoom")]
        [Tooltip("How much a pinch gesture affects zoom. Higher = more zoom per pinch pixel.")]
        [SerializeField] private float zoomSensitivity = 0.02f;

        [Header("Pan")]
        [Tooltip("How much a two-finger drag affects camera pan. Higher = more pan per pixel.")]
        [SerializeField] private float panSensitivity = 1.0f;
        [SerializeField] private float panLingerSec = 2f;

        [Header("Mouse (Editor)")]
        [SerializeField] private float mouseWheelZoomSpeed = 3f;

        private CameraRig _cameraRig;
        private UnityEngine.Camera _camera;

        private float _lastPinchDistance;
        private Vector2 _lastMidpoint;
        private bool _gestureActive = false;

        private float _panLingerTimer = 0f;

        /// <summary>True while a two-finger gesture is active. SwipeDetector checks this
        /// to suppress single-finger swipes during pinch/pan.</summary>
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

            if (_panLingerTimer > 0f)
            {
                _panLingerTimer -= Time.deltaTime;
                if (_panLingerTimer <= 0f && _cameraRig != null)
                {
                    _cameraRig.enabled = true;
                }
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

            // Two fingers on screen — suppress single-finger input
            IsTwoFingerGestureActive = true;

            Touch t0 = UnityEngine.Input.GetTouch(0);
            Touch t1 = UnityEngine.Input.GetTouch(1);

            float currentDistance = Vector2.Distance(t0.position, t1.position);
            Vector2 currentMidpoint = (t0.position + t1.position) * 0.5f;

            // Start of gesture — just record, don't apply
            if (!_gestureActive)
            {
                _lastPinchDistance = currentDistance;
                _lastMidpoint = currentMidpoint;
                _gestureActive = true;
                return;
            }

            // Compute deltas
            float pinchDelta = currentDistance - _lastPinchDistance;  // positive = fingers apart, negative = together
            float pinchMag = Mathf.Abs(pinchDelta);
            Vector2 panDelta = currentMidpoint - _lastMidpoint;
            float panMag = panDelta.magnitude;

            // Classify: is this primarily a pinch or a pan?
            // Use a threshold so small pinch movements during pan don't trigger zoom
            if (pinchMag > 3f && pinchMag > panMag * 0.5f)
            {
                // PINCH (zoom)
                // Fingers apart (pinchDelta > 0) → zoom IN → decrease ortho size → NEGATIVE delta
                // Fingers together (pinchDelta < 0) → zoom OUT → increase ortho size → POSITIVE delta
                // So we INVERT the pinch delta:
                float zoomChange = -pinchDelta * zoomSensitivity;
                ApplyZoom(zoomChange);
            }
            else if (panMag > 2f)
            {
                // PAN
                ApplyPan(panDelta);

                // Disable camera follow during pan
                if (_cameraRig != null && _cameraRig.enabled)
                {
                    _cameraRig.enabled = false;
                }
                _panLingerTimer = panLingerSec;
            }

            // Always update last positions
            _lastPinchDistance = currentDistance;
            _lastMidpoint = currentMidpoint;
        }

        private void ApplyZoom(float zoomDelta)
        {
            if (_cameraRig == null) return;
            // zoomDelta is NEGATIVE when zooming IN (fingers apart → decrease ortho size)
            // zoomDelta is POSITIVE when zooming OUT (fingers together → increase ortho size)
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

            _camera.transform.position += worldDelta;
        }

        private void HandleMouseWheel()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // Scroll up (positive) → zoom IN → decrease ortho size → NEGATIVE delta
                ApplyZoom(-scroll * mouseWheelZoomSpeed);
            }
#endif
        }
    }
}
