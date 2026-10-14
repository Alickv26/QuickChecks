using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
    /// Serialized with Newtonsoft.Json (handles List&lt;struct&gt; properly,
    /// unlike Unity's built-in JsonUtility).
    /// </summary>
    [Serializable]
    [JsonObject]
    public class GhostData
    {
        [JsonProperty] public string trackId;
        [JsonProperty] public string kartId;

        // Primary ranking field (lower = better). Tiebreaker = finishTimeMs.
        [JsonProperty] public int swipeCount;

        // Secondary ranking field. Lower = better. Used when swipeCount ties.
        [JsonProperty] public long finishTimeMs;

        // Mode the race was played in (solo, vs_cpu_easy, vs_cpu_hard, pass_play, online).
        [JsonProperty] public string mode;

        [JsonProperty] public List<string> powerUpsCollected = new();
        [JsonProperty] public List<SwipeEventEntry> swipeEvents = new();

        [Serializable]
        [JsonObject]
        public struct SwipeEventEntry
        {
            [JsonProperty] public long t;          // ms since race start
            [JsonProperty] public float x;         // screen position (normalized 0-1)
            [JsonProperty] public float y;
            [JsonProperty] public float dx;        // normalized direction
            [JsonProperty] public float dy;
            [JsonProperty] public float mag;       // impulse magnitude
        }
    }
}
