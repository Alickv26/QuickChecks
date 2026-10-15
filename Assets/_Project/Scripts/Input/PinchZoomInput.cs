using UnityEngine;
using QuickChecks.Camera;

namespace QuickChecks.Input
{
    /// <summary>
    /// Map-style pan and zoom for the camera.
    ///
    /// Two-finger gestures:
    /// - PINCH: fingers move toward/away from each other = zoom in/out
    /// - PAN: both fingers move in the same direction = move camera
    ///
    /// The gesture is classified each frame based on the ratio of:
    ///   pinch_delta (change in distance between fingers)
    ///   pan_delta (movement of the midpoint)
    ///
    /// If pinch_delta > pan_delta → it's a zoom
    /// If pan_delta > pinch_delta → it's a pan
    /// This prevents the random zoom that happens when panning with slightly uneven fingers.
    /// </summary>
    [RequireComponent(typeof(CameraRig))]
    public class PinchZoomInput : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] private float zoomSensitivity = 0.015f;

        [Header("Pan")]
        [SerializeField] private float panSensitivity = 0.015f;
        [SerializeField] private float panLingerSec = 2f;

        [Header("Mouse (Editor)")]
        [SerializeField] private float mouseWheelZoomSpeed = 3f;

        private CameraRig _cameraRig;
        private UnityEngine.Camera _camera;

        private float _lastPinchDistance;
        private Vector2 _lastMidpoint;
        private bool _gestureActive = false;

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
                return;
            }

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
            float pinchDelta = Mathf.Abs(currentDistance - _lastPinchDistance);
            Vector2 panDelta = currentMidpoint - _lastMidpoint;
            float panMag = panDelta.magnitude;

            // Classify: is this primarily a pinch or a pan?
            // Use a threshold ratio so small pinch movements during pan don't trigger zoom
            if (pinchDelta > 3f && pinchDelta > panMag * 0.5f)
            {
                // PINCH (zoom)
                float zoomChange = (currentDistance - _lastPinchDistance) * zoomSensitivity;
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
            _cameraRig.ApplyPinchZoom(zoomDelta);
        }

        private void ApplyPan(Vector2 screenDelta)
        {
            if (_camera == null) return;

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
