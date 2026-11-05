using UnityEngine;

namespace QuickChecks.Camera
{
    /// <summary>
    /// Follows the player kart with smoothing, applies pinch-zoom, and
    /// optionally responds to per-segment zoom hints from the active track.
    ///
    /// Pan offset (from PinchZoomInput) is applied AFTER this component's
    /// LateUpdate — so panning adds on top of the follow position without
    /// fighting it.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraRig : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform of the player kart. Set at race start.")]
        public Transform target;

        [Header("Follow Smoothing")]
        [Tooltip("Smoothing factor: 0 = no follow, 0.5 = smooth, 1 = instant snap.")]
        [Range(0f, 1f)] public float followSmoothing = 0.15f;

        [Header("Zoom")]
        [SerializeField] private QuickChecks.Input.InputSettings inputSettings;
        private float _currentZoom;
        private float _targetZoom;

        private UnityEngine.Camera _cam;
        private Vector3 _velocityRef;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _cam.orthographic = true;
            // Preserve whatever orthographicSize was set by BuildCamera (the track's defaultZoom),
            // instead of hard-coding 12f. The old code had a dead ternary `inputSettings != null ? 12f : 12f`
            // which always returned 12f, overriding the track's defaultZoom (14-18) and making the
            // camera start more zoomed-in than intended — which made pan/zoom feel more sensitive
            // (smaller viewport = same world-unit pan covers more screen).
            _currentZoom = _targetZoom = _cam.orthographicSize > 0f ? _cam.orthographicSize : 12f;
        }

        private void LateUpdate()
        {
            // Follow target (if set + enabled)
            if (target != null)
            {
                Vector3 desired = target.position;
                desired.z = transform.position.z;
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref _velocityRef, 1f - followSmoothing
                );
            }

            // Smooth zoom toward target
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * 10f);
            _cam.orthographicSize = _currentZoom;
        }

        /// <summary>
        /// Called by PinchZoomInput when user pinches.
        ///
        /// Direction convention:
        ///   zoomDelta NEGATIVE = zoom IN → decrease ortho size
        ///   zoomDelta POSITIVE = zoom OUT → increase ortho size
        /// </summary>
        public void ApplyPinchZoom(float zoomDelta)
        {
            float minZoom = inputSettings != null ? inputSettings.minCameraZoom : 4f;
            float maxZoom = inputSettings != null ? inputSettings.maxCameraZoom : 30f;

            _targetZoom = Mathf.Clamp(_targetZoom + zoomDelta, minZoom, maxZoom);

            // Partially snap current toward target for immediate feedback
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, 0.4f);
        }

        public void ResetToDefault(float defaultZoom)
        {
            _targetZoom = defaultZoom;
            _currentZoom = defaultZoom;
        }
    }
}
