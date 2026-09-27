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

        [Header("Editor Testing (mouse fallback)")]
        [Tooltip("Lower threshold for mouse swipes in editor — mice can't flick as fast as fingers. Only affects editor builds.")]
        [SerializeField] private float editorMinSwipeVelocity = 400f;

        [Tooltip("Max duration for mouse swipe (longer than touch — mouse drags are slower).")]
        [SerializeField] private float editorMaxSwipeDurationMs = 400f;

        [Tooltip("Min distance for mouse swipe (smaller than touch — easier to test).")]
        [SerializeField] private float editorMinSwipeDistance = 20f;

        [Header("Diagnostics")]
        [Tooltip("Log every mouse-down/up event with velocity/distance for debugging swipe feel.")]
        [SerializeField] private bool logAllSwipeAttempts = true;

        private int? _activeTouchId = null;
        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private Vector2 _lastTouchPos;
        private float _peakVelocity;

        private long _raceStartTimeMs;

        // Effective thresholds (touch or editor depending on platform)
        private float _minVelocity;
        private float _maxDurationMs;
        private float _minDistance;

        private void Awake()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // Use relaxed thresholds when testing with mouse.
            _minVelocity = editorMinSwipeVelocity;
            _maxDurationMs = editorMaxSwipeDurationMs;
            _minDistance = editorMinSwipeDistance;
#else
            _minVelocity = settings != null ? settings.minSwipeVelocity : 1000f;
            _maxDurationMs = settings != null ? settings.maxSwipeDurationMs : 220f;
            _minDistance = settings != null ? settings.minSwipeDistance : 50f;
#endif
        }

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
            // Ignore mouse input if touch is active (touch takes priority).
            if (UnityEngine.Input.touchCount > 0 && _activeTouchId != -1)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _activeTouchId = -1; // sentinel for mouse
                _touchStartPos = UnityEngine.Input.mousePosition;
                _lastTouchPos = UnityEngine.Input.mousePosition;
                _touchStartTime = Time.realtimeSinceStartup;
                _peakVelocity = 0f;

                if (logAllSwipeAttempts)
                {
                    Debug.Log($"[Swipe] Mouse down at {_touchStartPos}. Threshold: v>={_minVelocity}px/s, " +
                              $"dur<={_maxDurationMs}ms, dist>={_minDistance}px");
                }
            }
            else if (_activeTouchId == -1 && UnityEngine.Input.GetMouseButton(0))
            {
                Vector2 cur = UnityEngine.Input.mousePosition;
                float moveDist = (cur - _lastTouchPos).magnitude;
                // crude velocity estimate (px/s)
                float v = moveDist / Mathf.Max(Time.deltaTime, 0.001f);
                if (v > _peakVelocity)
                {
                    _peakVelocity = v;
                    if (logAllSwipeAttempts && v > 100f)
                    {
                        Debug.Log($"[Swipe] Tracking: peakV={_peakVelocity:F0}px/s, dist={(cur - _touchStartPos).magnitude:F0}px");
                    }
                }
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

            bool tooSlow = _peakVelocity < _minVelocity;
            bool tooLong = durationMs > _maxDurationMs;
            bool tooShort = distance < _minDistance;

            if (logAllSwipeAttempts)
            {
                string verdict = tooSlow ? "TOO SLOW" :
                                  tooLong ? "TOO LONG" :
                                  tooShort ? "TOO SHORT" : "VALID";
                Debug.Log($"[Swipe] Eval: peakV={_peakVelocity:F0}px/s, dur={durationMs:F0}ms, " +
                          $"dist={distance:F0}px -> {verdict}");
            }

            if (tooSlow || tooLong || tooShort)
            {
                // Not a valid swipe — silently ignore (player must flick faster).
                return;
            }

            Vector2 direction = (endPos - _touchStartPos).normalized;
            float magnitude = Mathf.Min(
                _peakVelocity * (settings != null ? settings.velocityToImpulseScale : 1.5f),
                settings != null ? settings.maxImpulseMagnitude : 1500f
            );

            var data = new SwipeData
            {
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartTimeMs,
                screenPosition = _touchStartPos,
                direction = direction,
                magnitude = magnitude,
                rawVelocityPxS = _peakVelocity
            };

            if (swipeEvent != null)
            {
                swipeEvent.Raise(data);
            }
            else
            {
                Debug.LogError("[SwipeDetector] swipeEvent is null! Cannot raise swipe event. " +
                               "Wire it up in the Inspector or via the PrototypeSceneBuilder.");
            }
        }
    }
}
