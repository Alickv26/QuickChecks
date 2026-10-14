using UnityEngine;
using QuickChecks.Input;
using QuickChecks.Core;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost. In SYNCHRONIZED mode (default), the ghost does
    /// its next recorded swipe immediately when the player swipes — creating a
    /// turn-taking feel where both karts move together.
    ///
    /// The ghost mirrors the player's previous best run, swipe for swipe.
    /// When the player swipes, the ghost immediately swipes in the direction
    /// it recorded during its best lap. This makes it feel like racing against
    /// yourself — the ghost follows the exact line you took on your best run.
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        public enum PlaybackMode
        {
            Synchronized,  // Ghost swipes immediately when player swipes (turn-taking)
            RealTime       // Ghost swipes at recorded timestamps (parallel race)
        }

        [SerializeField] private GameEventSO<SwipeData> ghostSwipeEvent;  // event to raise for ghost kart
        [SerializeField] private GameEventSO<SwipeData> playerSwipeEvent; // event to listen to (player's swipes)
        [SerializeField] private Transform ghostKart;

        [Header("Playback")]
        [Tooltip("Synchronized = ghost swipes immediately when player swipes (turn-taking feel). " +
                 "RealTime = ghost swipes at original timestamps (parallel race).")]
        [SerializeField] private PlaybackMode mode = PlaybackMode.Synchronized;

        private GhostData _data;
        private int _nextEventIndex = 0;
        private long _playbackStartUnixMs;
        private bool _isPlaying = false;
        private bool _pendingSwipe = false;  // simple flag instead of timer

        public bool IsPlaying => _isPlaying;
        public int EventsReplayed => _nextEventIndex;
        public int TotalEvents => _data?.swipeEvents?.Count ?? 0;

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _nextEventIndex = 0;
            _playbackStartUnixMs = startUnixMs;
            _isPlaying = true;
            _pendingSwipe = false;

            if (mode == PlaybackMode.Synchronized)
            {
                if (playerSwipeEvent != null)
                {
                    playerSwipeEvent.Register(OnPlayerSwipe);
                    Debug.Log($"[GhostPlayer] Synchronized playback started. Will replay {TotalEvents} events. " +
                              "Ghost will swipe immediately when player swipes.");
                }
                else
                {
                    Debug.LogWarning("[GhostPlayer] Synchronized mode but no playerSwipeEvent assigned — ghost won't move.");
                }
            }
            else if (mode == PlaybackMode.RealTime)
            {
                Debug.Log($"[GhostPlayer] Real-time playback started. Will replay {TotalEvents} events at recorded timestamps.");
            }
        }

        public void StopPlayback()
        {
            _isPlaying = false;
            if (mode == PlaybackMode.Synchronized && playerSwipeEvent != null)
            {
                playerSwipeEvent.Unregister(OnPlayerSwipe);
            }
        }

        private void OnPlayerSwipe(SwipeData playerSwipe)
        {
            if (!_isPlaying || _data == null) return;
            if (_nextEventIndex >= _data.swipeEvents.Count) return;

            // Set the pending flag — Update() will emit the ghost swipe on the next frame.
            // Using a flag instead of a timer avoids the bug where syncDelaySec=0
            // caused the timer check (> 0f) to never fire.
            _pendingSwipe = true;
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            // Handle pending synchronized swipe — emit on next frame after player swiped
            if (_pendingSwipe)
            {
                EmitNextSwipe();
                _pendingSwipe = false;
            }

            // Real-time mode: emit any events whose timestamp has passed
            if (mode == PlaybackMode.RealTime)
            {
                long elapsed = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _playbackStartUnixMs;
                while (_nextEventIndex < _data.swipeEvents.Count)
                {
                    var entry = _data.swipeEvents[_nextEventIndex];
                    if (entry.t > elapsed) break;
                    EmitNextSwipe();
                }
            }

            if (_nextEventIndex >= _data.swipeEvents.Count)
            {
                _isPlaying = false;
                Debug.Log($"[GhostPlayer] Playback complete. Replayed {EventsReplayed} events.");
                if (mode == PlaybackMode.Synchronized && playerSwipeEvent != null)
                {
                    playerSwipeEvent.Unregister(OnPlayerSwipe);
                }
            }
        }

        private void EmitNextSwipe()
        {
            if (_data == null || _nextEventIndex >= _data.swipeEvents.Count) return;

            var entry = _data.swipeEvents[_nextEventIndex];
            var swipe = new SwipeData
            {
                timestampMs = entry.t,
                screenPosition = new Vector2(entry.x, entry.y),
                direction = new Vector2(entry.dx, entry.dy),
                magnitude = entry.mag,
                rawVelocityPxS = entry.mag
            };

            if (ghostSwipeEvent != null)
            {
                ghostSwipeEvent.Raise(swipe);
                Debug.Log($"[GhostPlayer] Emitted ghost swipe {EventsReplayed + 1}/{TotalEvents}: " +
                          $"dir=({entry.dx:F2}, {entry.dy:F2}), mag={entry.mag:F0}");
            }
            else
            {
                Debug.LogWarning("[GhostPlayer] ghostSwipeEvent is null — can't emit swipe.");
            }

            _nextEventIndex++;
        }

        private void OnDisable()
        {
            StopPlayback();
        }
    }
}
