using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost by emitting synthetic SwipeData events at
    /// the timestamps captured during the original race. Listened to by
    /// the ghost kart's own KartController (with separate swipeEvent SO
    /// to avoid cross-contamination with player input).
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> ghostSwipeEvent;
        [SerializeField] private Transform ghostKart;

        private GhostData _data;
        private int _nextEventIndex = 0;
        private long _playbackStartUnixMs;
        private bool _isPlaying = false;

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _nextEventIndex = 0;
            _playbackStartUnixMs = startUnixMs;
            _isPlaying = true;
        }

        public void StopPlayback()
        {
            _isPlaying = false;
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            long elapsed = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _playbackStartUnixMs;

            while (_nextEventIndex < _data.swipeEvents.Count)
            {
                var entry = _data.swipeEvents[_nextEventIndex];
                if (entry.t > elapsed) break;

                // Emit synthetic swipe.
                var swipe = new SwipeData
                {
                    timestampMs = entry.t,
                    screenPosition = new Vector2(entry.x, entry.y),
                    direction = new Vector2(entry.dx, entry.dy),
                    magnitude = entry.mag,
                    rawVelocityPxS = entry.mag // Approximation, fine for ghosts
                };
                ghostSwipeEvent?.Raise(swipe);

                _nextEventIndex++;
            }

            if (_nextEventIndex >= _data.swipeEvents.Count)
            {
                _isPlaying = false;
                // Ghost finished its race.
            }
        }
    }
}
