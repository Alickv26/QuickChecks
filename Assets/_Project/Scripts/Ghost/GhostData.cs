using System;
using System.Collections.Generic;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// JSON-serializable recording of a finished race. Captures every swipe
    /// event so it can be replayed deterministically on any device.
    ///
    /// Updated for turn-based model: swipeCount is now a first-class field,
    /// since the global leaderboard ranks by fewest swipes (not fastest time).
    ///
    /// Target size: 5-8 KB per race.
    /// </summary>
    [Serializable]
    public class GhostData
    {
        public string trackId;
        public string kartId;

        // Primary ranking field (lower = better). Tiebreaker = finishTimeMs.
        public int swipeCount;

        // Secondary ranking field. Lower = better. Used when swipeCount ties.
        public long finishTimeMs;

        // Mode the race was played in (solo, vs_cpu_easy, vs_cpu_hard, pass_play, online).
        public string mode;

        public List<string> powerUpsCollected = new();
        public List<SwipeEventEntry> swipeEvents = new();

        [Serializable]
        public struct SwipeEventEntry
        {
            public long t;          // ms since race start
            public float x;         // screen position (normalized 0-1)
            public float y;
            public float dx;        // normalized direction
            public float dy;
            public float mag;       // impulse magnitude
        }
    }
}
