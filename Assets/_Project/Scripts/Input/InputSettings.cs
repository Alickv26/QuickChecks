using UnityEngine;

namespace QuickChecks.Input
{
    /// <summary>
    /// Tunable thresholds for swipe detection. Stored as a ScriptableObject
    /// so designers can tweak without code changes, and so per-device
    /// calibration can override at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Input Settings", fileName = "InputSettings")]
    public class InputSettings : ScriptableObject
    {
        [Header("Swipe Detection Thresholds")]
        [Tooltip("Minimum peak velocity (px/s) for a swipe to count as valid. Below this = ignored.")]
        public float minSwipeVelocity = 1000f;

        [Tooltip("Maximum duration (ms) from touch-down to lift. Above this = treated as drag, not swipe.")]
        public float maxSwipeDurationMs = 220f;

        [Tooltip("Minimum distance (px) the finger must travel. Below this = tap, not swipe.")]
        public float minSwipeDistance = 50f;

        [Header("Impulse Scaling")]
        [Tooltip("Swipe velocity is multiplied by this to get impulse magnitude. " +
                 "Lower = smaller impulses per swipe (kart moves shorter distances).")]
        public float velocityToImpulseScale = 0.4f;

        [Tooltip("Hard cap on impulse magnitude (units/sec of velocity). " +
                 "Kart at 250 u/s travels ~4 units per frame at 60fps — feels right for a 40-unit-wide track.")]
        public float maxImpulseMagnitude = 250f;

        [Header("Pinch Zoom")]
        [Tooltip("Minimum ortho camera size (closest zoom, for tight sections).")]
        public float minCameraZoom = 5f;

        [Tooltip("Maximum ortho camera size (furthest zoom, for long straights).")]
        public float maxCameraZoom = 20f;

        [Tooltip("Pinch sensitivity — higher = more zoom per pinch gesture.")]
        public float pinchSensitivity = 0.15f;
    }
}
