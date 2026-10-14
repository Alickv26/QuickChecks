using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// JSON-serializable recording of a finished race.
    ///
    /// TWO replay systems:
    /// 1. Swipe events (for leaderboard ranking — swipe count is the primary metric)
    /// 2. Position samples (for ghost replay — guarantees exact path reproduction)
    ///
    /// The position-based replay is the authoritative ghost playback method.
    /// It records the kart's position + rotation every 50ms during the race,
    /// then replays them by setting transform.position directly.
    /// This guarantees the ghost follows the EXACT same path, including wall stops,
    /// regardless of physics differences.
    /// </summary>
    [Serializable]
    [JsonObject]
    public class GhostData
    {
        [JsonProperty] public string trackId;
        [JsonProperty] public string kartId;

        [JsonProperty] public int swipeCount;
        [JsonProperty] public long finishTimeMs;
        [JsonProperty] public string mode;

        // Swipe events — used for analytics + leaderboard validation
        [JsonProperty] public List<SwipeEventEntry> swipeEvents = new();

        // Position samples — used for ghost replay (authoritative path)
        // Sampled at 50ms intervals during the race
        [JsonProperty] public List<PositionSample> positionSamples = new();

        [JsonProperty] public List<string> powerUpsCollected = new();

        [Serializable]
        [JsonObject]
        public struct SwipeEventEntry
        {
            [JsonProperty] public long t;
            [JsonProperty] public float dx;
            [JsonProperty] public float dy;
            [JsonProperty] public float mag;
        }

        /// <summary>
        /// A position sample recorded during the race.
        /// Replayed by setting transform.position directly — no physics needed.
        /// </summary>
        [Serializable]
        [JsonObject]
        public struct PositionSample
        {
            [JsonProperty] public long t;       // ms since race start
            [JsonProperty] public float x;       // world X
            [JsonProperty] public float y;       // world Y
            [JsonProperty] public float rot;     // Z rotation in degrees
        }
    }
}
