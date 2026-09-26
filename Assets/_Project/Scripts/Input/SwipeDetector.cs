using System;
using UnityEngine;

namespace QuickChecks.Input
{
    /// <summary>
    /// Data captured when a valid swipe is detected.
    /// - Screen position is the touch start point (in screen px)
    /// - Direction is normalized (in screen space — caller converts to world)
    /// - Magnitude is the impulse strength (already scaled + capped)
    /// - Timestamp is in ms since race start (for ghost recording)
    /// </summary>
    [Serializable]
    public struct SwipeData
    {
        public long timestampMs;
        public Vector2 screenPosition;
        public Vector2 direction;       // normalized
        public float magnitude;         // 0..maxImpulseMagnitude
        public float rawVelocityPxS;   // unscaled peak velocity (for analytics/haptics)
    }

    /// <summary>
    /// Detects valid swipes on touch screens. A "valid" swipe must be:
    /// - Fast (peak velocity >= InputSettings.minSwipeVelocity)
    /// - Short (duration <= InputSettings.maxSwipeDurationMs)
    /// - Long enough (distance >= InputSettings.minSwipeDistance)
    /// Slow drags are ignored — this enforces the "flick, not steer" feel.
    ///
    /// Raises an event via the SwipeEventSO ScriptableObject.
    /// </summary>
    public class SwipeDetector : MonoBehaviour
    {
        [SerializeField] private InputSettings settings;
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;

        private int? _activeTouchId = null;
        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private Vector2 _lastTouchPos;
        private float _peakVelocity;

        private long _raceStartTimeMs;

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#endif
            HandleTouchInput();
        }

        public void SetRaceStartTime(long unixMs)
        {
            _raceStartTimeMs = unixMs;
        }

        private void HandleTouchInput()
        {
            if (UnityEngine.Input.touchCount == 0) return;

            // Look for a new touch (began phase) that we haven't claimed yet.
            if (_activeTouchId == null)
            {
                foreach (var touch in UnityEngine.Input.touches)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        _activeTouchId = touch.fingerId;
                        _touchStartPos = touch.position;
                        _lastTouchPos = touch.position;
                        _touchStartTime = Time.realtimeSinceStartup;
                        _peakVelocity = 0f;
                        break;
                    }
                }
            }

            if (_activeTouchId == null) return;

            // Track the active touch.
            foreach (var touch in UnityEngine.Input.touches)
            {
                if (touch.fingerId != _activeTouchId.Value) continue;

                switch (touch.phase)
                {
                    case TouchPhase.Moved:
                        // Update peak velocity (deltaPosition / deltaTime).
                        float dt = touch.deltaTime;
                        if (dt > 0f)
                        {
                            float v = touch.deltaPosition.magnitude / dt;
                            if (v > _peakVelocity) _peakVelocity = v;
                        }
                        _lastTouchPos = touch.position;
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EvaluateSwipe(touch.position);
                        _activeTouchId = null;
                        break;
                }
            }
        }

        private void HandleMouseInput()
        {
            // Mouse fallback for editor testing.
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _activeTouchId = -1; // sentinel
                _touchStartPos = UnityEngine.Input.mousePosition;
                _lastTouchPos = UnityEngine.Input.mousePosition;
                _touchStartTime = Time.realtimeSinceStartup;
                _peakVelocity = 0f;
            }
            else if (_activeTouchId == -1 && UnityEngine.Input.GetMouseButton(0))
            {
                Vector2 cur = UnityEngine.Input.mousePosition;
                float moveDist = (cur - _lastTouchPos).magnitude;
                // crude velocity estimate
                _peakVelocity = Mathf.Max(_peakVelocity, moveDist / Mathf.Max(Time.deltaTime, 0.001f));
                _lastTouchPos = cur;
            }
            else if (_activeTouchId == -1 && UnityEngine.Input.GetMouseButtonUp(0))
            {
                EvaluateSwipe(UnityEngine.Input.mousePosition);
                _activeTouchId = null;
            }
        }

        private void EvaluateSwipe(Vector2 endPos)
        {
            float durationMs = (Time.realtimeSinceStartup - _touchStartTime) * 1000f;
            float distance = (endPos - _touchStartPos).magnitude;

            bool tooSlow = _peakVelocity < settings.minSwipeVelocity;
            bool tooLong = durationMs > settings.maxSwipeDurationMs;
            bool tooShort = distance < settings.minSwipeDistance;

            if (tooSlow || tooLong || tooShort)
            {
                // Not a valid swipe — silently ignore (player must flick faster).
                return;
            }

            Vector2 direction = (endPos - _touchStartPos).normalized;
            float magnitude = Mathf.Min(
                _peakVelocity * settings.velocityToImpulseScale,
                settings.maxImpulseMagnitude
            );

            var data = new SwipeData
            {
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartTimeMs,
                screenPosition = _touchStartPos,
                direction = direction,
                magnitude = magnitude,
                rawVelocityPxS = _peakVelocity
            };

            swipeEvent?.Raise(data);
        }
    }
}
