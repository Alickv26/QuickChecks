using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// JSON-serializable recording of a finished race. Captures every swipe
    /// event so it can be replayed deterministically on any device.
    /// Target size: 5-8 KB per race.
    /// </summary>
    [Serializable]
    public class GhostData
    {
        public string trackId;
        public string kartId;
        public long finishTimeMs;
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
