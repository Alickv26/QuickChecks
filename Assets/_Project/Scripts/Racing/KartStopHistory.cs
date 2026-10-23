using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Tracks the kart's recent stop positions (where the kart was stationary after each swipe).
    /// Used by the BlackHole obstacle to rewind the kart along its actual swipe path instead of
    /// a random toss.
    ///
    /// This is a small, focused component separate from GhostRecorder (which records everything
    /// for ghost replay). StopHistory is a runtime-only ring buffer of the last N stops —
    /// just enough for obstacles to look back a few moves.
    ///
    /// Lifecycle:
    ///   - On swipe fired (registered to the kart's SwipeEvent): mark "next stop will be recorded"
    ///   - On FixedUpdate: if kart transitions from moving to stopped AND a swipe was fired since
    ///     the last recorded stop, push the position onto the ring buffer.
    ///   - GetStopBack(n) returns the n-th most recent stop (0 = last stop, 1 = stop before that, etc.)
    /// </summary>
    public class KartStopHistory : MonoBehaviour
    {
        [Tooltip("Maximum stops to keep in the ring buffer.")]
        [SerializeField] private int maxStops = 16;

        [Tooltip("Velocity below which the kart is considered 'stopped'.")]
        [SerializeField] private float stoppedThreshold = 8f;

        private KartController _kart;
        private readonly List<StopEntry> _stops = new(16);
        private bool _swipeSinceLastStop = false;
        private bool _wasMoving = false;

        public struct StopEntry
        {
            public Vector3 position;
            public float rotation;
            public int swipeIndex;  // 0-based swipe count at the time of this stop
        }

        /// <summary>Number of stops currently in the buffer.</summary>
        public int Count => _stops.Count;

        /// <summary>True if at least one stop has been recorded.</summary>
        public bool HasHistory => _stops.Count > 0;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            // Record the starting position as stop 0 (before any swipe).
            // This way GetStopBack(0) returns the spawn point if no swipes have happened yet.
            _stops.Add(new StopEntry
            {
                position = transform.position,
                rotation = transform.eulerAngles.z,
                swipeIndex = -1
            });
        }

        private void Start()
        {
            if (_kart == null) _kart = GetComponent<KartController>();
        }

        /// <summary>
        /// Called by RaceSceneController (or any other system) when a swipe is registered on
        /// the kart. Marks "the next stop should be recorded" so we don't pollute the history
        /// with stops that came from power-ups or wall bounces without a swipe.
        /// </summary>
        public void NotifySwipe()
        {
            _swipeSinceLastStop = true;
        }

        private void FixedUpdate()
        {
            if (_kart == null) return;

            bool isMoving = _kart.Velocity.magnitude > stoppedThreshold;

            if (_wasMoving && !isMoving && _swipeSinceLastStop)
            {
                // Kart just stopped after a swipe — record this position.
                PushStop();
                _swipeSinceLastStop = false;
            }

            _wasMoving = isMoving;
        }

        private void PushStop()
        {
            var entry = new StopEntry
            {
                position = transform.position,
                rotation = transform.eulerAngles.z,
                swipeIndex = _stops.Count > 0 ? _stops[_stops.Count - 1].swipeIndex + 1 : 0
            };

            _stops.Add(entry);

            // Trim to maxStops (keep the most recent ones).
            if (_stops.Count > maxStops)
            {
                _stops.RemoveAt(0);
            }

            Debug.Log($"[KartStopHistory] Stop #{_stops.Count - 1} at {entry.position} " +
                      $"(swipeIndex={entry.swipeIndex}).");
        }

        /// <summary>
        /// Returns the n-th stop back from the most recent one.
        ///   backCount = 0 → most recent stop (where the kart currently is, or last stopped)
        ///   backCount = 1 → the stop before that (one swipe back)
        ///   backCount = 2 → two swipes back
        /// Returns the spawn position if backCount exceeds available history.
        /// </summary>
        public StopEntry GetStopBack(int backCount)
        {
            if (_stops.Count == 0) return default;
            int idx = _stops.Count - 1 - backCount;
            if (idx < 0) idx = 0;
            return _stops[idx];
        }

        /// <summary>
        /// Returns the stop position N swipes back, capped to the earliest recorded.
        /// Equivalent to GetStopBack(backCount).position but convenient.
        /// </summary>
        public Vector3 GetPositionBack(int backCount)
        {
            return GetStopBack(backCount).position;
        }

        /// <summary>Clears all history (used on race reset).</summary>
        public void Clear()
        {
            _stops.Clear();
            _stops.Add(new StopEntry
            {
                position = transform.position,
                rotation = transform.eulerAngles.z,
                swipeIndex = -1
            });
            _swipeSinceLastStop = false;
            _wasMoving = false;
        }
    }
}
