using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Records swipes, position samples, AND stop positions during a race.
    ///
    /// Stop positions are the key for ghost replay — they record exactly where
    /// the kart stopped after each swipe. The ghost then replays by advancing
    /// through these stop positions one at a time, creating a true 1:1 swipe
    /// correspondence.
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private Transform kartTransform;
        [SerializeField] private Racing.KartController kartController;

        [Tooltip("Velocity below which the kart is considered 'stopped'.")]
        [SerializeField] private float stoppedThreshold = 10f;

        private GhostData _data;
        private long _raceStartUnixMs;
        private bool _isRecording;
        private float _lastSampleTime;
        private const float SAMPLE_INTERVAL = 0.05f;

        // Stop position tracking
        private bool _wasMoving = false;
        private int _swipeCount = 0;

        public GhostData CurrentData => _data;

        public void BeginRace(string track, string kart)
        {
            _data = new GhostData
            {
                trackId = track,
                kartId = kart,
                swipeEvents = new List<GhostData.SwipeEventEntry>(64),
                positionSamples = new List<GhostData.PositionSample>(400),
                stopPositions = new List<GhostData.StopPosition>(32)
            };
            _raceStartUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _isRecording = true;
            _lastSampleTime = 0f;
            _wasMoving = false;
            _swipeCount = 0;

            // Record the starting position as stop position 0 (before any swipe)
            if (kartTransform != null)
            {
                _data.stopPositions.Add(new GhostData.StopPosition
                {
                    x = kartTransform.position.x,
                    y = kartTransform.position.y,
                    rot = kartTransform.eulerAngles.z,
                    swipeIndex = -1  // starting position, before any swipe
                });
                Debug.Log($"[GhostRecorder] Recorded start position: {kartTransform.position}");
            }
        }

        public GhostData EndRace(long finishTimeMs)
        {
            _isRecording = false;
            if (_data == null) return null;
            _data.finishTimeMs = finishTimeMs;
            _data.swipeCount = _data.swipeEvents.Count;

            // Record final position as last stop
            if (kartTransform != null && _data.stopPositions.Count > 0)
            {
                var lastStop = _data.stopPositions[_data.stopPositions.Count - 1];
                // Only add if we moved since the last stop
                float dist = Vector2.Distance(
                    new Vector2(lastStop.x, lastStop.y),
                    new Vector2(kartTransform.position.x, kartTransform.position.y));
                if (dist > 0.5f)
                {
                    _data.stopPositions.Add(new GhostData.StopPosition
                    {
                        x = kartTransform.position.x,
                        y = kartTransform.position.y,
                        rot = kartTransform.eulerAngles.z,
                        swipeIndex = _swipeCount - 1
                    });
                }
            }

            Debug.Log($"[GhostRecorder] Race ended. {_data.swipeCount} swipes, " +
                      $"{_data.positionSamples.Count} position samples, " +
                      $"{_data.stopPositions.Count} stop positions.");
            return _data;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
            // Find kart controller if not assigned
            if (kartController == null && kartTransform != null)
            {
                kartController = kartTransform.GetComponent<Racing.KartController>();
            }
        }

        private void OnDisable()
        {
            if (swipeEvent != null && _isSubscribed)
            {
                swipeEvent.Unregister(OnSwipe);
                _isSubscribed = false;
            }
        }

        private bool _isSubscribed = false;

        private void TrySubscribe()
        {
            if (swipeEvent != null && !_isSubscribed)
            {
                swipeEvent.Register(OnSwipe);
                _isSubscribed = true;
            }
        }

        private void OnSwipe(SwipeData swipe)
        {
            if (!_isRecording || _data == null) return;

            _data.swipeEvents.Add(new GhostData.SwipeEventEntry
            {
                t = swipe.timestampMs,
                dx = swipe.direction.x,
                dy = swipe.direction.y,
                mag = swipe.magnitude
            });
            _swipeCount++;
        }

        private void FixedUpdate()
        {
            if (!_isRecording || _data == null || kartTransform == null) return;

            // Sample position at regular intervals
            if (Time.time - _lastSampleTime >= SAMPLE_INTERVAL)
            {
                _lastSampleTime = Time.time;
                long t = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _raceStartUnixMs;
                _data.positionSamples.Add(new GhostData.PositionSample
                {
                    t = t,
                    x = kartTransform.position.x,
                    y = kartTransform.position.y,
                    rot = kartTransform.eulerAngles.z
                });
            }

            // Detect stop positions (kart transitions from moving to stopped)
            if (kartController != null)
            {
                bool isMoving = kartController.Velocity.magnitude > stoppedThreshold;

                if (_wasMoving && !isMoving && _swipeCount > 0)
                {
                    // Kart just stopped after a swipe — record this position
                    _data.stopPositions.Add(new GhostData.StopPosition
                    {
                        x = kartTransform.position.x,
                        y = kartTransform.position.y,
                        rot = kartTransform.eulerAngles.z,
                        swipeIndex = _swipeCount - 1
                    });
                    Debug.Log($"[GhostRecorder] Stop #{_data.stopPositions.Count - 1} recorded at " +
                              $"{kartTransform.position} (after swipe {_swipeCount})");
                }

                _wasMoving = isMoving;
            }
        }

        public void RecordPowerUpCollected(string powerUpId)
        {
            if (_data == null) return;
            _data.powerUpsCollected.Add(powerUpId);
        }
    }
}
