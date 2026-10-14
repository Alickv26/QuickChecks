using UnityEngine;
using QuickChecks.Input;
using QuickChecks.Core;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost. Supports two playback modes:
    ///
    /// 1. SYNCHRONIZED (default for prototype): Ghost does its next recorded swipe
    ///    immediately after the player swipes. This creates a "follow-along" feel
    ///    where the ghost mirrors the player's swipe count.
    ///
    /// 2. REAL_TIME: Ghost plays swipes at their original timestamps (independent
    ///    of player). Used for online multiplayer where each player races in
    ///    parallel and you want to see the opponent's actual timing.
    ///
    /// Subscribes to the player's swipeEvent (in SYNCHRONIZED mode) to detect
    /// when the player swipes, then advances the ghost by one swipe.
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        public enum PlaybackMode
        {
            Synchronized,  // Ghost swipes after player swipes (follow-along)
            RealTime       // Ghost swipes at recorded timestamps
        }

        [SerializeField] private GameEventSO<SwipeData> ghostSwipeEvent;  // event to raise for ghost kart
        [SerializeField] private GameEventSO<SwipeData> playerSwipeEvent; // event to listen to (player's swipes)
        [SerializeField] private Transform ghostKart;

        [Header("Playback")]
        [Tooltip("Synchronized = ghost swipes after player swipes. " +
                 "RealTime = ghost swipes at original timestamps.")]
        [SerializeField] private PlaybackMode mode = PlaybackMode.Synchronized;

        [Tooltip("Delay (seconds) between player's swipe and ghost's response swipe. " +
                 "0 = immediate (ghost swipes same frame), 0.3 = small lag for visual clarity. " +
                 "Default 0 = immediate sync, ghost swipes right after player.")]
        [SerializeField] private float syncDelaySec = 0f;

        private GhostData _data;
        private int _nextEventIndex = 0;
        private long _playbackStartUnixMs;
        private bool _isPlaying = false;
        private float _pendingSwipeTimer = -1f;  // > 0 means a delayed ghost swipe is pending

        public bool IsPlaying => _isPlaying;
        public int EventsReplayed => _nextEventIndex;
        public int TotalEvents => _data?.swipeEvents?.Count ?? 0;

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _nextEventIndex = 0;
            _playbackStartUnixMs = startUnixMs;
            _isPlaying = true;
            _pendingSwipeTimer = -1f;

            if (mode == PlaybackMode.Synchronized && playerSwipeEvent != null)
            {
                playerSwipeEvent.Register(OnPlayerSwipe);
                Debug.Log($"[GhostPlayer] Synchronized playback started. Will replay {TotalEvents} events after player swipes.");
            }
            else if (mode == PlaybackMode.RealTime)
            {
                Debug.Log($"[GhostPlayer] Real-time playback started. Will replay {TotalEvents} events at recorded timestamps.");
            }
            else
            {
                Debug.LogWarning("[GhostPlayer] Synchronized mode but no playerSwipeEvent assigned — ghost won't move.");
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

            // Schedule the ghost's next swipe after the sync delay.
            // This creates a "follow-along" feel — player swipes, then ghost swipes.
            _pendingSwipeTimer = syncDelaySec;
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            // Handle pending synchronized swipe
            if (_pendingSwipeTimer > 0f)
            {
                _pendingSwipeTimer -= Time.deltaTime;
                if (_pendingSwipeTimer <= 0f)
                {
                    EmitNextSwipe();
                    _pendingSwipeTimer = -1f;
                }
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
