using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Records every swipe AND position sample during a race.
    /// Position samples are taken every 50ms for exact ghost replay.
    /// </summary>
    public class GhostRecorder : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private Transform kartTransform;

        private GhostData _data;
        private long _raceStartUnixMs;
        private bool _isRecording;
        private float _lastSampleTime;
        private const float SAMPLE_INTERVAL = 0.05f; // 50ms = 20Hz

        public GhostData CurrentData => _data;

        public void BeginRace(string track, string kart)
        {
            _data = new GhostData
            {
                trackId = track,
                kartId = kart,
                swipeEvents = new List<GhostData.SwipeEventEntry>(64),
                positionSamples = new List<GhostData.PositionSample>(400) // ~20s of racing at 20Hz
            };
            _raceStartUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _isRecording = true;
            _lastSampleTime = 0f;
        }

        public GhostData EndRace(long finishTimeMs)
        {
            _isRecording = false;
            if (_data == null) return null;
            _data.finishTimeMs = finishTimeMs;
            _data.swipeCount = _data.swipeEvents.Count;
            return _data;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
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
        }

        public void RecordPowerUpCollected(string powerUpId)
        {
            if (_data == null) return;
            _data.powerUpsCollected.Add(powerUpId);
        }
    }
}
