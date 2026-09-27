using UnityEngine;

namespace QuickChecks.Camera
{
    /// <summary>
    /// Follows the player kart with smoothing, applies pinch-zoom, and
    /// optionally responds to per-segment zoom hints from the active track.
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
        private float _hintZoomOverride = -1f; // set by track segments
        private float _hintOverrideExpireTime = 0f;

        private UnityEngine.Camera _cam;
        private Vector3 _velocityRef; // for SmoothDamp

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _cam.orthographic = true;
            _currentZoom = _targetZoom = inputSettings != null ? 12f : 12f;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Smooth follow.
            Vector3 desired = target.position;
            desired.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocityRef, 1f - followSmoothing
            );

            // Decide target zoom: player pinch override expires after 2s of no pinch.
            // Track segment hints take over after that.
            if (_hintOverrideExpireTime > Time.time)
            {
                _targetZoom = _hintZoomOverride;
            }
            // Pinch zoom is applied directly via ApplyPinchZoom — see below.

            // Smooth zoom.
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * 8f);
            _cam.orthographicSize = _currentZoom;
        }

        /// <summary>
        /// Called by PinchZoomInput when user pinches. Player override takes precedence.
        /// </summary>
        public void ApplyPinchZoom(float zoomDelta)
        {
            if (inputSettings == null) return;
            _targetZoom = Mathf.Clamp(
                _targetZoom - zoomDelta * inputSettings.pinchSensitivity * 50f,
                inputSettings.minCameraZoom,
                inputSettings.maxCameraZoom
            );
            // Player override lasts 2 seconds.
            _hintOverrideExpireTime = Time.time + 2f;
        }

        /// <summary>
        /// Called by TrackSegment triggers when kart enters a segment with a zoom hint.
        /// </summary>
        public void ApplyZoomHint(float zoom)
        {
            _hintZoomOverride = zoom;
            _hintOverrideExpireTime = Time.time + 5f; // hint holds for 5s, then player takes over
        }

        public void ResetToDefault(float defaultZoom)
        {
            _targetZoom = defaultZoom;
            _currentZoom = defaultZoom;
            _hintZoomOverride = -1f;
            _hintOverrideExpireTime = 0f;
        }
    }
}
